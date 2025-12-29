using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Commands.TerminateToken;

public class TerminateTokenCommandHandler : IRequestHandler<TerminateTokenCommand, TerminateTokenResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<TerminateTokenCommandHandler> _logger;

    public TerminateTokenCommandHandler(
        IUnitOfWork uow,
        ILogger<TerminateTokenCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TerminateTokenResult> Handle(TerminateTokenCommand request, CancellationToken ct)
    {
        _logger.LogInformation(
            "[TERMINATE-TOKEN] Terminating token. ProcessId={ProcessId} TokenId={TokenId} Reason={Reason}",
            request.ProcessId,
            request.TokenId,
            request.Reason);

        await _uow.BeginTransactionAsync(ct);

        try
        {
            var token = await _uow.Tokens.GetByIdAsync(request.TokenId, ct);
            if (token == null)
            {
                _logger.LogWarning("[TERMINATE-TOKEN] Token not found. TokenId={TokenId}", request.TokenId);
                await _uow.RollbackTransactionAsync(ct);
                return new TerminateTokenResult(request.TokenId, false, "Token not found");
            }

            if (token.State == TokenState.Completed)
            {
                _logger.LogWarning("[TERMINATE-TOKEN] Cannot terminate completed token. TokenId={TokenId}", request.TokenId);
                await _uow.RollbackTransactionAsync(ct);
                return new TerminateTokenResult(request.TokenId, false, "Cannot terminate completed token");
            }

            if (token.State == TokenState.Terminated)
            {
                _logger.LogWarning("[TERMINATE-TOKEN] Token already terminated. TokenId={TokenId}", request.TokenId);
                await _uow.RollbackTransactionAsync(ct);
                return new TerminateTokenResult(request.TokenId, false, "Token already terminated");
            }

            // Terminate the token (this publishes TokenTerminatedEvent)
            token.Terminate(request.Reason);

            await _uow.CommitTransactionAsync(ct);

            _logger.LogInformation(
                "[TERMINATE-TOKEN] Token terminated successfully. ProcessId={ProcessId} TokenId={TokenId}",
                request.ProcessId,
                request.TokenId);

            return new TerminateTokenResult(request.TokenId, true, request.Reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TERMINATE-TOKEN] Error terminating token. ProcessId={ProcessId} TokenId={TokenId}",
                request.ProcessId, request.TokenId);
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }
    }
}