using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Commands.SetActivityInstance;

public sealed class SetActivityInstanceCommandHandler : IRequestHandler<SetActivityInstanceCommand, SetActivityInstanceResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<SetActivityInstanceCommandHandler> _logger;

    public SetActivityInstanceCommandHandler(IUnitOfWork uow, ILogger<SetActivityInstanceCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SetActivityInstanceResult> Handle(SetActivityInstanceCommand request, CancellationToken cancellationToken)
    {
        await _uow.BeginTransactionAsync(cancellationToken);
        try
        {
            var token = await _uow.Tokens.GetByIdAsync(request.TokenId, cancellationToken);
            if (token is null || token.ProcessId != request.ProcessId)
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                return new SetActivityInstanceResult(request.TokenId, false, "Token not found");
            }

            token.SetActivityInstance(request.ActivityInstanceId);

            await _uow.CommitTransactionAsync(cancellationToken);
            return new SetActivityInstanceResult(request.TokenId, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SET-ACTIVITY-INSTANCE] Failed. TokenId={TokenId}", request.TokenId);
            await _uow.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

