using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Commands.SetArrivedViaFlow;

public sealed class SetArrivedViaFlowCommandHandler : IRequestHandler<SetArrivedViaFlowCommand, SetArrivedViaFlowResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<SetArrivedViaFlowCommandHandler> _logger;

    public SetArrivedViaFlowCommandHandler(IUnitOfWork uow, ILogger<SetArrivedViaFlowCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SetArrivedViaFlowResult> Handle(SetArrivedViaFlowCommand request, CancellationToken cancellationToken)
    {
        await _uow.BeginTransactionAsync(cancellationToken);
        try
        {
            var token = await _uow.Tokens.GetByIdAsync(request.TokenId, cancellationToken);
            if (token is null || token.ProcessId != request.ProcessId)
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                return new SetArrivedViaFlowResult(request.TokenId, false, "Token not found");
            }

            token.SetArrivedVia(request.FlowId);

            await _uow.CommitTransactionAsync(cancellationToken);
            return new SetArrivedViaFlowResult(request.TokenId, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SET-ARRIVED-VIA] Failed. TokenId={TokenId}", request.TokenId);
            await _uow.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

