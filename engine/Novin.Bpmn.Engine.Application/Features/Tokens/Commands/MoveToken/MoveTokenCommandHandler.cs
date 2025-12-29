using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Commands.MoveToken;

/// <summary>
/// Moves an ACTIVE token to the next BPMN element.
/// IMPORTANT: This handler ONLY moves the token.
/// Routing/splitting/joining/completing must be done by element handlers.
/// </summary>
public sealed class MoveTokenCommandHandler : IRequestHandler<MoveTokenCommand, MoveTokenResult>
{
    private readonly IUnitOfWork _uow;
    private readonly IProcessExecutionRecorder _executionRecorder;
    private readonly ILogger<MoveTokenCommandHandler> _logger;

    public MoveTokenCommandHandler(
        IUnitOfWork uow,
        IProcessExecutionRecorder executionRecorder,
        ILogger<MoveTokenCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _executionRecorder = executionRecorder ?? throw new ArgumentNullException(nameof(executionRecorder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MoveTokenResult> Handle(MoveTokenCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NextElementId))
            return new MoveTokenResult(request.TokenId, false, "NextElementId is empty.");

        _logger.LogDebug(
            "[MOVE-TOKEN] Request. ProcessId={ProcessId} TokenId={TokenId} NextElementId={NextElementId} ViaFlowId={ViaFlowId}",
            request.ProcessId, request.TokenId, request.NextElementId, request.ViaFlowId);

        Process? process = null;
        Token? token = null;

        string? fromElementId = null;
        string? arrivedViaToCurrent = null;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            // 1) Load process (needed for recorder + validation)
            process = await _uow.Processes.GetByIdAsync(request.ProcessId, ct);
            if (process is null)
            {
                await _uow.RollbackTransactionAsync(ct);
                return new MoveTokenResult(request.TokenId, false, "Process not found");
            }

            // 2) Load token
            token = await _uow.Tokens.GetByIdAsync(request.TokenId, ct);
            if (token is null || token.ProcessId != request.ProcessId)
            {
                await _uow.RollbackTransactionAsync(ct);
                _logger.LogWarning("[MOVE-TOKEN] Token not found. TokenId={TokenId} ProcessId={ProcessId}",
                    request.TokenId, request.ProcessId);
                return new MoveTokenResult(request.TokenId, false, "Token not found");
            }

            // 3) Validate state
            if (token.State != TokenState.Active)
            {
                await _uow.RollbackTransactionAsync(ct);
                _logger.LogWarning("[MOVE-TOKEN] Token not Active. TokenId={TokenId} State={State}",
                    request.TokenId, token.State);
                return new MoveTokenResult(request.TokenId, false, $"Token is {token.State}, expected Active");
            }

            // capture BEFORE move (because MoveTo will change CurrentElementId/ArrivedViaFlowId)
            fromElementId = token.CurrentElementId;
            arrivedViaToCurrent = token.ArrivedViaFlowId;

            // 4) Move (domain emits TokenMovedEvent internally if you have it)
            token.MoveTo(request.NextElementId, request.ViaFlowId);

            // 5) Commit movement
            await _uow.CommitTransactionAsync(ct);

            _logger.LogInformation(
                "[MOVE-TOKEN] Moved. TokenId={TokenId} From={From} To={To} ViaFlowId={ViaFlowId}",
                request.TokenId, fromElementId, request.NextElementId, request.ViaFlowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MOVE-TOKEN] Failed. TokenId={TokenId}", request.TokenId);
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }

        // 6) Best-effort execution recording (OUTSIDE trx to avoid nested transaction problems)
        // Record the element we just LEFT (fromElementId).
      
        return new MoveTokenResult(request.TokenId, true);
    }
}
