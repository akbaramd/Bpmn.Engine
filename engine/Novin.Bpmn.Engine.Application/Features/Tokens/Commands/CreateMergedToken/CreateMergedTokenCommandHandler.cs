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
        Guid result = Guid.Empty;

        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            var process = await _uow.Processes.GetByIdAsync(cmd.ProcessId, trxCt)
                         ?? throw new InvalidOperationException($"Process not found. ProcessId={cmd.ProcessId}");

            // Idempotency check: if merged token already created for this join, return existing
            var mergedTokenKey = GatewayScopeKeys.GwMergedToken(cmd.GatewayId, cmd.ScopeId);
            var existingMergedTokenIdStr = process.Variables.TryGetValue(mergedTokenKey, out var existing) 
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

            // Create new merged token
            var parentIds = cmd.ParentTokenIds?.Where(id => id != Guid.Empty).Distinct().ToList() 
                          ?? new List<Guid>();

            // Determine executability: merged token is executable only if at least one parent was executable
            // If all parent tokens are non-executable (or no parent tokens), merged token is non-executable
            var mergedIsExecutable = false;
            if (parentIds.Count > 0)
            {
                // Load parent tokens to check their executability
                foreach (var parentId in parentIds)
                {
                    var parentToken = await _uow.Tokens.GetByIdAsync(parentId, trxCt);
                    if (parentToken?.IsExecutable == true)
                    {
                        mergedIsExecutable = true;
                        break; // At least one parent is executable
                    }
                }
            }
            // If no parent tokens or all are non-executable, mergedIsExecutable remains false

            var mergedToken = new Token(
                processId: cmd.ProcessId,
                startElementId: cmd.GatewayId,
                parentTokenIds: parentIds);

            // Set arrived via flow if provided
            if (!string.IsNullOrWhiteSpace(cmd.ArrivedViaFlowId))
            {
                mergedToken.SetArrivedVia(cmd.ArrivedViaFlowId);
            }

            // Set executability: merged token inherits executability from parent tokens
            // If all parents are non-executable, merged token is non-executable
            if (!mergedIsExecutable)
            {
                mergedToken.MarkNonExecutable("All parent tokens were non-executable");
            }

            // ScopeId is cleared because join scope is complete
            mergedToken.ClearScope();

            // Activate the merged token (publishes TokenActivatedEvent which will trigger processing)
            mergedToken.Activate();

            // Save merged token
            await _uow.Tokens.AddAsync(mergedToken, trxCt);
            process.AddToken(mergedToken.Id);

            // Store merged token ID for idempotency (key: gateway + scope)
            process.SetVariable(mergedTokenKey, mergedToken.Id.ToString());

            await _uow.Processes.UpdateAsync(process, trxCt);

            _logger.LogInformation(
                "[CREATE-MERGED-TOKEN] Merged token created and activated. " +
                "ProcessId={ProcessId} GatewayId={GatewayId} ScopeId={ScopeId} MergedTokenId={MergedTokenId} " +
                "ParentCount={ParentCount} IsExecutable={IsExecutable}",
                cmd.ProcessId, cmd.GatewayId, cmd.ScopeId, mergedToken.Id, parentIds.Count, mergedToken.IsExecutable);

            result = mergedToken.Id;
        }, ct);

        return result;
    }
}
