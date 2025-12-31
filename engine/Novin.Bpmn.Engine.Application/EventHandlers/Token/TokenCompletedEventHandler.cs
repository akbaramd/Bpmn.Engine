using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

public sealed class TokenCompletedEventHandler : INotificationHandler<TokenCompletedEvent>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<TokenCompletedEventHandler> _logger;

    public TokenCompletedEventHandler(IUnitOfWork uow, ILogger<TokenCompletedEventHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(TokenCompletedEvent e, CancellationToken ct)
    {
        // بهترین حالت: همه چیز داخل یک تراکنش کوتاه
        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            // 1) Load process
            var process = await _uow.Processes.GetByIdAsync(e.ProcessId, trxCt);
            if (process is null)
            {
                _logger.LogWarning("[TOKEN-COMPLETED] Process not found. ProcessId={ProcessId}", e.ProcessId);
                return;
            }

            // Idempotency: process already terminal
            if (process.State is ProcessState.Completed or ProcessState.Failed or ProcessState.Terminated)
                return;

            // 2) Load all tokens of process (authoritative decision)
            // اگر در UoW متد ندارید، از repo خودتون استفاده کنید
            var tokens = (await _uow.Tokens.GetByProcessIdAsync(process.Id, trxCt)).ToList();

            // فقط executable ها معیار پایان process
            var hasOpenExecutableTokens = tokens.Any(t =>
                t.IsExecutable &&
                (t.State == TokenState.Created || t.State == TokenState.Active || t.State == TokenState.Waiting));

            if (hasOpenExecutableTokens)
            {
                _logger.LogDebug(
                    "[TOKEN-COMPLETED] Process still has open executable tokens. ProcessId={ProcessId}",
                    process.Id);
                return;
            }

            // 3) No open executable tokens => process complete
            process.Complete();

            await _uow.Processes.UpdateAsync(process, trxCt);

            _logger.LogInformation(
                "[TOKEN-COMPLETED] Process completed. ProcessId={ProcessId}",
                process.Id);

        }, ct);
    }
}
