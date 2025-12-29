using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Commands.AssignScopeToToken;

public sealed class AssignScopeToTokenCommandHandler : IRequestHandler<AssignScopeToTokenCommand, AssignScopeToTokenResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<AssignScopeToTokenCommandHandler> _logger;

    public AssignScopeToTokenCommandHandler(IUnitOfWork uow, ILogger<AssignScopeToTokenCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AssignScopeToTokenResult> Handle(AssignScopeToTokenCommand request, CancellationToken cancellationToken)
    {
        await _uow.BeginTransactionAsync(cancellationToken);
        try
        {
            var token = await _uow.Tokens.GetByIdAsync(request.TokenId, cancellationToken);
            if (token is null || token.ProcessId != request.ProcessId)
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                return new AssignScopeToTokenResult(request.TokenId, false, "Token not found");
            }

            token.SetScope(request.ScopeId);

            await _uow.CommitTransactionAsync(cancellationToken);
            return new AssignScopeToTokenResult(request.TokenId, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ASSIGN-SCOPE] Failed. TokenId={TokenId}", request.TokenId);
            await _uow.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

