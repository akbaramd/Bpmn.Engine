using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Commands.ClearTokenLocalVariables;

public sealed class ClearTokenLocalVariablesCommandHandler : IRequestHandler<ClearTokenLocalVariablesCommand, ClearTokenLocalVariablesResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ClearTokenLocalVariablesCommandHandler> _logger;

    public ClearTokenLocalVariablesCommandHandler(IUnitOfWork uow, ILogger<ClearTokenLocalVariablesCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ClearTokenLocalVariablesResult> Handle(ClearTokenLocalVariablesCommand request, CancellationToken cancellationToken)
    {
        await _uow.BeginTransactionAsync(cancellationToken);
        try
        {
            var token = await _uow.Tokens.GetByIdAsync(request.TokenId, cancellationToken);
            if (token is null || token.ProcessId != request.ProcessId)
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                return new ClearTokenLocalVariablesResult(request.TokenId, false, "Token not found");
            }

            token.ClearLocalVariables();

            await _uow.CommitTransactionAsync(cancellationToken);
            return new ClearTokenLocalVariablesResult(request.TokenId, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CLEAR-TOKEN-VARS] Failed. TokenId={TokenId}", request.TokenId);
            await _uow.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

