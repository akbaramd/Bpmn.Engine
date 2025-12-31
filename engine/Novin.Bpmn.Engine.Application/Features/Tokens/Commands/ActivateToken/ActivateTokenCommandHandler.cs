using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Commands.ActivateToken;

public class ActivateTokenCommandHandler : IRequestHandler<ActivateTokenCommand, ActivateTokenResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ActivateTokenCommandHandler> _logger;

    public ActivateTokenCommandHandler(
        IUnitOfWork uow,
        ILogger<ActivateTokenCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ActivateTokenResult> Handle(ActivateTokenCommand request, CancellationToken ct)
    {
        _logger.LogInformation(
            "[ACTIVATE-TOKEN] Activating token. ProcessId={ProcessId} TokenId={TokenId} ArrivedViaFlowId={ArrivedViaFlowId}",
            request.ProcessId,
            request.TokenId,
            request.ArrivedViaFlowId ?? "null");

        await _uow.BeginTransactionAsync(ct);

        try
        {
            var token = await _uow.Tokens.GetByIdAsync(request.TokenId, ct);
            if (token == null)
            {
                _logger.LogWarning("[ACTIVATE-TOKEN] Token not found. TokenId={TokenId}", request.TokenId);
                await _uow.RollbackTransactionAsync(ct);
                return new ActivateTokenResult(request.TokenId, false, "Token not found");
            }

            if (token.State != TokenState.Created)
            {
                _logger.LogWarning(
                    "[ACTIVATE-TOKEN] Token not in Created state. TokenId={TokenId} State={State}",
                    request.TokenId,
                    token.State);
                await _uow.RollbackTransactionAsync(ct);
                return new ActivateTokenResult(request.TokenId, false, $"Token in {token.State} state");
            }

            // Set arrived via flow if provided
            if (!string.IsNullOrEmpty(request.ArrivedViaFlowId))
            {
                token.SetArrivedVia(request.ArrivedViaFlowId);
            }

            // Activate the token (this publishes TokenProcessingRequestedEvent)
            token.Activate();

            await _uow.CommitTransactionAsync(ct);

            _logger.LogInformation(
                "[ACTIVATE-TOKEN] Token activated successfully. ProcessId={ProcessId} TokenId={TokenId}",
                request.ProcessId,
                request.TokenId);

            return new ActivateTokenResult(request.TokenId, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ACTIVATE-TOKEN] Error activating token. ProcessId={ProcessId} TokenId={TokenId}",
                request.ProcessId, request.TokenId);
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }
    }
}