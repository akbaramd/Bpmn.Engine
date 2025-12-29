using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Commands.ClearArrivedViaFlow;

public sealed class ClearArrivedViaFlowCommandHandler : IRequestHandler<ClearArrivedViaFlowCommand, ClearArrivedViaFlowResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ClearArrivedViaFlowCommandHandler> _logger;

    public ClearArrivedViaFlowCommandHandler(IUnitOfWork uow, ILogger<ClearArrivedViaFlowCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ClearArrivedViaFlowResult> Handle(ClearArrivedViaFlowCommand request, CancellationToken cancellationToken)
    {
        await _uow.BeginTransactionAsync(cancellationToken);
        try
        {
            var token = await _uow.Tokens.GetByIdAsync(request.TokenId, cancellationToken);
            if (token is null || token.ProcessId != request.ProcessId)
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                return new ClearArrivedViaFlowResult(request.TokenId, false, "Token not found");
            }

            token.ClearArrivedVia();

            await _uow.CommitTransactionAsync(cancellationToken);
            return new ClearArrivedViaFlowResult(request.TokenId, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CLEAR-ARRIVED-VIA] Failed. TokenId={TokenId}", request.TokenId);
            await _uow.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

