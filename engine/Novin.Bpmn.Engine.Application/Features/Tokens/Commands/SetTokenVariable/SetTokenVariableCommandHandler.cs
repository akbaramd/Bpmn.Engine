using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Commands.SetTokenVariable;

public sealed class SetTokenVariableCommandHandler : IRequestHandler<SetTokenVariableCommand, SetTokenVariableResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<SetTokenVariableCommandHandler> _logger;

    public SetTokenVariableCommandHandler(IUnitOfWork uow, ILogger<SetTokenVariableCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SetTokenVariableResult> Handle(SetTokenVariableCommand request, CancellationToken cancellationToken)
    {
        await _uow.BeginTransactionAsync(cancellationToken);
        try
        {
            var token = await _uow.Tokens.GetByIdAsync(request.TokenId, cancellationToken);
            if (token is null || token.ProcessId != request.ProcessId)
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                return new SetTokenVariableResult(request.TokenId, false, "Token not found");
            }

            token.SetVariable(request.Name, request.Value);

            await _uow.CommitTransactionAsync(cancellationToken);
            return new SetTokenVariableResult(request.TokenId, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SET-TOKEN-VAR] Failed. TokenId={TokenId}", request.TokenId);
            await _uow.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

