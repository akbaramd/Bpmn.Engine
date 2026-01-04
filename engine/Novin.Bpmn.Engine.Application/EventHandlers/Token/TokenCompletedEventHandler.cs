using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

// Zeebe-like: process completes when no OPEN EXECUTABLE tokens remain.
public sealed class TokenCompletedEventHandler :
    INotificationHandler<TokenCompletedEvent>,
    INotificationHandler<TokenTerminatedEvent>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<TokenCompletedEventHandler> _logger;

    public TokenCompletedEventHandler(IUnitOfWork uow, ILogger<TokenCompletedEventHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task Handle(TokenCompletedEvent e, CancellationToken ct)
        => EvaluateProcessCompletionAsync(e.ProcessId, ct);

    public Task Handle(TokenTerminatedEvent e, CancellationToken ct)
        => EvaluateProcessCompletionAsync(e.ProcessId, ct);

    private async Task EvaluateProcessCompletionAsync(Guid processId, CancellationToken ct)
    {
        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            var process = await _uow.Processes.GetByIdAsync(processId, trxCt);
            if (process is null)
            {
                _logger.LogWarning("[PROC-END] Process not found. ProcessId={ProcessId}", processId);
                return;
            }

            if (process.State is ProcessState.Completed or ProcessState.Failed or ProcessState.Terminated)
                return;

            var tokens = await _uow.Tokens.GetByProcessIdAsync(process.Id, trxCt);

            // OPEN executable tokens (Forked must block completion)
            var hasOpenExecutable = tokens.Any(t =>
                IsExecutableToken(t) &&
                (t.State == TokenState.Created ||
                 t.State == TokenState.Active ||
                 t.State == TokenState.Waiting ||
                 t.State == TokenState.Forked));

            if (hasOpenExecutable)
                return;

            process.Complete();
            await _uow.Processes.UpdateAsync(process, trxCt);

            _logger.LogInformation("[PROC-END] Process completed. ProcessId={ProcessId}", process.Id);
        }, ct);
    }

    // No hard dependency on Token.IsExecutable (works if property exists)
    private static readonly Func<Token, bool> _isExecutable = BuildIsExecutable();

    private static Func<Token, bool> BuildIsExecutable()
    {
        var p = typeof(Token).GetProperty("IsExecutable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p is null || p.PropertyType != typeof(bool)) return static _ => true;

        return t =>
        {
            try { return (bool)p.GetValue(t)!; }
            catch { return true; }
        };
    }

    private static bool IsExecutableToken(Token token) => _isExecutable(token);
}
