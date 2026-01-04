using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Commands.CreateMergedToken;

/// <summary>
/// Handler for CreateMergedTokenCommand.
/// Creates a merged token after join satisfaction with idempotency support.
/// </summary>
public sealed class CreateMergedTokenCommandHandler : IRequestHandler<CreateMergedTokenCommand, Guid>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<CreateMergedTokenCommandHandler> _logger;

    public CreateMergedTokenCommandHandler(
        IUnitOfWork uow,
        ILogger<CreateMergedTokenCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(CreateMergedTokenCommand cmd, CancellationToken ct)
    {
        const int maxRetries = 3;
        int retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                Guid result = Guid.Empty;

                await _uow.ExecuteInTransactionAsync(async trxCt =>
                {
                    // ✅ CRITICAL: Reload process in transaction to get latest state
                    // This ensures we see the most recent mergedTokenKey value even if another
                    // concurrent transaction just set it
                    var process = await _uow.Processes.GetByIdAsync(cmd.ProcessId, trxCt)
                                 ?? throw new InvalidOperationException($"Process not found. ProcessId={cmd.ProcessId}");

                    // ✅ Idempotency check: if merged token already created for this join, return existing
                    // This check happens INSIDE the transaction, so we see committed changes
                    var mergedTokenKey = GatewayScopeKeys.GwMergedToken(cmd.GatewayId, cmd.ScopeId);
                    var existingMergedTokenIdStr = process.TryGetVariable<string>(mergedTokenKey, out var existing) 
                        ? existing?.ToString() 
                        : null;

                    if (!string.IsNullOrWhiteSpace(existingMergedTokenIdStr) &&
                        Guid.TryParse(existingMergedTokenIdStr, out var existingMergedTokenId))
                    {
                        // Verify token still exists
                        var existingToken = await _uow.Tokens.GetByIdAsync(existingMergedTokenId, trxCt);
                        if (existingToken != null)
                        {
                            _logger.LogInformation(
                                "[CREATE-MERGED-TOKEN] Merged token already exists (idempotency). " +
                                "ProcessId={ProcessId} GatewayId={GatewayId} ScopeId={ScopeId} MergedTokenId={MergedTokenId}",
                                cmd.ProcessId, cmd.GatewayId, cmd.ScopeId, existingMergedTokenId);
                            result = existingMergedTokenId;
                            return;
                        }
                        // Token was deleted, continue to create new one
                        _logger.LogWarning(
                            "[CREATE-MERGED-TOKEN] Stored merged token ID not found, creating new. " +
                            "ProcessId={ProcessId} GatewayId={GatewayId} ScopeId={ScopeId} ExpectedTokenId={ExpectedTokenId}",
                            cmd.ProcessId, cmd.GatewayId, cmd.ScopeId, existingMergedTokenId);
                    }

                    // ✅ CRITICAL: At this point, we've confirmed no merged token exists
                    // We're inside a transaction, so concurrent transactions should be serialized
                    // by the database isolation level (Repeatable Read or Serializable)

                    // Create new merged token
                    var mergedToken = new Token(
                        processId: cmd.ProcessId,
                        startElementId: cmd.GatewayId,
                        parentTokenId: cmd.ParentTokenId);

                    // Collect ArrivedViaFlowIds from parent token
                    var allFlowIds = new HashSet<string>(StringComparer.Ordinal);
                    
                    if (cmd.ParentTokenId.HasValue && cmd.ParentTokenId.Value != Guid.Empty)
                    {
                        var parentToken = await _uow.Tokens.GetByIdAsync(cmd.ParentTokenId.Value, trxCt);
                        if (parentToken != null)
                        {
                            foreach (var flowId in parentToken.ArrivedViaFlowIds)
                            {
                                if (!string.IsNullOrWhiteSpace(flowId))
                                {
                                    allFlowIds.Add(flowId);
                                }
                            }
                        }
                    }
                    
                    // Set all collected flow IDs
                    if (allFlowIds.Count > 0)
                    {
                        mergedToken.SetArrivedViaFlowIds(allFlowIds);
                    }

                    // ScopeId is cleared because join scope is complete
                    mergedToken.ClearScope();

                    // Activate the merged token (publishes TokenActivatedEvent which will trigger processing)
                    mergedToken.Activate();

                    // Save merged token
                    await _uow.Tokens.AddAsync(mergedToken, trxCt);
                    process.AddToken(mergedToken.Id);

                    // ✅ CRITICAL: Store merged token ID for idempotency (key: gateway + scope)
                    // This MUST be set in the same transaction as token creation
                    process.SetVariable(mergedTokenKey, mergedToken.Id.ToString());

                    await _uow.Processes.UpdateAsync(process, trxCt);

                    _logger.LogInformation(
                        "[CREATE-MERGED-TOKEN] Merged token created and activated. " +
                        "ProcessId={ProcessId} GatewayId={GatewayId} ScopeId={ScopeId} MergedTokenId={MergedTokenId} " +
                        "ParentTokenId={ParentTokenId}",
                        cmd.ProcessId, cmd.GatewayId, cmd.ScopeId, mergedToken.Id, cmd.ParentTokenId);

                    result = mergedToken.Id;
                }, ct);

                return result;
            }
            catch (Exception ex) when (retryCount < maxRetries - 1)
            {
                // ✅ Retry on concurrency conflicts (e.g., DbUpdateConcurrencyException, unique constraint violations)
                retryCount++;
                _logger.LogWarning(
                    "[CREATE-MERGED-TOKEN] Concurrency conflict detected, retrying. " +
                    "ProcessId={ProcessId} GatewayId={GatewayId} ScopeId={ScopeId} RetryCount={RetryCount} Error={Error}",
                    cmd.ProcessId, cmd.GatewayId, cmd.ScopeId, retryCount, ex.Message);
                
                // Small delay before retry to allow other transaction to complete
                await Task.Delay(50 * retryCount, ct);
            }
        }

        // If all retries failed, check one more time if merged token was created by another transaction
        Guid finalCheck = Guid.Empty;
        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            var process = await _uow.Processes.GetByIdAsync(cmd.ProcessId, trxCt);
            if (process == null) return;

            var mergedTokenKey = GatewayScopeKeys.GwMergedToken(cmd.GatewayId, cmd.ScopeId);
            var existingMergedTokenIdStr = process.TryGetVariable<string>(mergedTokenKey, out var existing) 
                ? existing?.ToString() 
                : null;

            if (!string.IsNullOrWhiteSpace(existingMergedTokenIdStr) &&
                Guid.TryParse(existingMergedTokenIdStr, out var existingMergedTokenId))
            {
                var existingToken = await _uow.Tokens.GetByIdAsync(existingMergedTokenId, trxCt);
                if (existingToken != null)
                {
                    _logger.LogInformation(
                        "[CREATE-MERGED-TOKEN] Found merged token after retries. " +
                        "ProcessId={ProcessId} GatewayId={GatewayId} ScopeId={ScopeId} MergedTokenId={MergedTokenId}",
                        cmd.ProcessId, cmd.GatewayId, cmd.ScopeId, existingMergedTokenId);
                    finalCheck = existingMergedTokenId;
                }
            }
        }, ct);

        if (finalCheck != Guid.Empty)
            return finalCheck;

        throw new InvalidOperationException(
            $"Failed to create merged token after {maxRetries} retries. " +
            $"ProcessId={cmd.ProcessId} GatewayId={cmd.GatewayId} ScopeId={cmd.ScopeId}");
    }
}
