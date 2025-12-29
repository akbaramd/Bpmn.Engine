using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Commands.ClearActivityInstance;

public sealed class ClearActivityInstanceCommandHandler : IRequestHandler<ClearActivityInstanceCommand, ClearActivityInstanceResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ClearActivityInstanceCommandHandler> _logger;

    public ClearActivityInstanceCommandHandler(IUnitOfWork uow, ILogger<ClearActivityInstanceCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ClearActivityInstanceResult> Handle(ClearActivityInstanceCommand request, CancellationToken cancellationToken)
    {
        await _uow.BeginTransactionAsync(cancellationToken);
        try
        {
            var token = await _uow.Tokens.GetByIdAsync(request.TokenId, cancellationToken);
            if (token is null || token.ProcessId != request.ProcessId)
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                return new ClearActivityInstanceResult(request.TokenId, false, "Token not found");
            }

            token.ClearActivityInstance();

            await _uow.CommitTransactionAsync(cancellationToken);
            return new ClearActivityInstanceResult(request.TokenId, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CLEAR-ACTIVITY-INSTANCE] Failed. TokenId={TokenId}", request.TokenId);
            await _uow.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

