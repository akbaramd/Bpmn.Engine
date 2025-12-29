using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Commands.RetryToken;

public sealed class RetryTokenCommandHandler : IRequestHandler<RetryTokenCommand, RetryTokenResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<RetryTokenCommandHandler> _logger;

    public RetryTokenCommandHandler(IUnitOfWork uow, ILogger<RetryTokenCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RetryTokenResult> Handle(RetryTokenCommand request, CancellationToken cancellationToken)
    {
        await _uow.BeginTransactionAsync(cancellationToken);
        try
        {
            var token = await _uow.Tokens.GetByIdAsync(request.TokenId, cancellationToken);
            if (token is null || token.ProcessId != request.ProcessId)
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                return new RetryTokenResult(request.TokenId, false, "Token not found");
            }

            if (token.State != TokenState.Failed)
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                return new RetryTokenResult(request.TokenId, false, $"Token is {token.State}, expected Failed");
            }

            token.Retry();

            await _uow.CommitTransactionAsync(cancellationToken);
            return new RetryTokenResult(request.TokenId, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RETRY-TOKEN] Failed. TokenId={TokenId}", request.TokenId);
            await _uow.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

