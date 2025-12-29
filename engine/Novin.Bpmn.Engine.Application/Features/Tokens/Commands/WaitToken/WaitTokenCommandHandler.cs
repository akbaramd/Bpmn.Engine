using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Commands.WaitToken;

public sealed class WaitTokenCommandHandler : IRequestHandler<WaitTokenCommand, WaitTokenResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<WaitTokenCommandHandler> _logger;

    public WaitTokenCommandHandler(IUnitOfWork uow, ILogger<WaitTokenCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WaitTokenResult> Handle(WaitTokenCommand request, CancellationToken cancellationToken)
    {
        await _uow.BeginTransactionAsync(cancellationToken);

        try
        {
            var token = await _uow.Tokens.GetByIdAsync(request.TokenId, cancellationToken);
            if (token is null || token.ProcessId != request.ProcessId)
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                return new WaitTokenResult(request.TokenId, false, "Token not found");
            }

            if (token.State != TokenState.Active)
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                return new WaitTokenResult(request.TokenId, false, $"Token is {token.State}, expected Active");
            }

            token.Wait(request.Reason);

            await _uow.CommitTransactionAsync(cancellationToken);
            return new WaitTokenResult(request.TokenId, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WAIT-TOKEN] Failed. TokenId={TokenId}", request.TokenId);
            await _uow.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

