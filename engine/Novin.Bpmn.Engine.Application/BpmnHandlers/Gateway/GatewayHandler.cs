using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.CreateMergedToken;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;
using NodeState = Novin.Bpmn.Engine.Domain.Entities.NodeState;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

public sealed class GatewayHandler : BpmnElementHandlerBase
{
    private readonly IGatewaySplitService _split;
    private readonly IVariableMappingService _variableMapping;
    private readonly ITokenRepository _tokenRepository;
    private readonly INodeInstanceRepository _nodeRepository;
    private readonly IUnitOfWork _uow;
    private readonly IMediator _mediator;
    private readonly ILogger<GatewayHandler> _logger;

    
    public GatewayHandler(
        IGatewaySplitService split,
        IVariableMappingService variableMapping,
        IFeelExpressionEvaluator feel,
        ILogger<GatewayHandler> logger,
        ITokenRepository tokenRepository,
        INodeInstanceRepository nodeRepository,
        IUnitOfWork uow,
        IMediator mediator)
        : base(feel, logger)
    {
        _split = split ?? throw new ArgumentNullException(nameof(split));
        _variableMapping = variableMapping ?? throw new ArgumentNullException(nameof(variableMapping));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenRepository = tokenRepository ?? throw new ArgumentNullException(nameof(tokenRepository));
        _nodeRepository = nodeRepository ?? throw new ArgumentNullException(nameof(nodeRepository));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public override bool CanHandle(BpmnFlowElement element) => element is BpmnGateway;

   // ============================================================
// TOKEN PROCESS: Join/Merge ONLY (token-driven, no node usage)
// ============================================================
public override async Task<TokenProcessResult> TokenProcessAsync(
    Process process,
    Token token,
    BpmnFlowElement element,
    BpmnRuntimeContext ctx,
    bool isResume,
    CancellationToken ct)
{
    if (process is null) throw new ArgumentNullException(nameof(process));
    if (token is null) throw new ArgumentNullException(nameof(token));
    if (element is null) throw new ArgumentNullException(nameof(element));
    if (ctx is null) throw new ArgumentNullException(nameof(ctx));

    // terminal safety
    if (token.State is TokenState.Terminated or TokenState.Failed)
        return TokenProcessResult.NoOp;

    var gateway = (BpmnGateway)element;

    // ✅ CRITICAL: Use gateway.id (not token.CurrentElementId) for join logic
    // token.CurrentElementId might be wrong (e.g., flowId instead of gateway id)
    var gwId = gateway.id ?? throw new InvalidOperationException(
        $"Gateway element must have an id. CurrentElementId={token.CurrentElementId}");

    // ✅ Defensive check: warn if token.CurrentElementId doesn't match gateway.id
    // This helps diagnose navigation issues
    if (!string.Equals(token.CurrentElementId, gwId, StringComparison.Ordinal))
    {
        _logger.LogWarning(
            "[JOIN] Token CurrentElementId mismatch! Gw={Gw} TokenCurrentEl={CurrentEl} TokenId={TokenId}",
            gwId, token.CurrentElementId, token.Id);
    }

    // Join/Merge candidates only: incoming > 1 && outgoing == 1
    var incoming = ctx.Model.GetIncomingSequenceFlows(ctx.BpmnProcessId, gwId);
    var outgoing = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, gwId);

    // ✅ Load-bearing log: helps diagnose if join logic is skipped
    _logger.LogInformation(
        "[JOIN] TokenId={TokenId} Gw={Gw} CurrentEl={CurrentEl} Scope={Scope} incoming={In} outgoing={Out}",
        token.Id, gwId, token.CurrentElementId, token.ScopeId, incoming.Count, outgoing.Count);

    if (incoming.Count <= 1 || outgoing.Count != 1)
    {
        _logger.LogDebug(
            "[JOIN] Not a join candidate. Gw={Gw} incoming={In} outgoing={Out}",
            gwId, incoming.Count, outgoing.Count);
        return TokenProcessResult.Continue;
    }

    // Join correlation requires ScopeId
    if (token.ScopeId is null || token.ScopeId == Guid.Empty)
    {
        _logger.LogWarning(
            "[JOIN] Token missing ScopeId. Gw={Gw} TokenId={TokenId} CurrentEl={CurrentEl}",
            gwId, token.Id, token.CurrentElementId);
        return TokenProcessResult.Continue;
    }

    var scopeId = token.ScopeId.Value;

    // Join state keys (gateway+scope)
    var closedKey   = GatewayScopeKeys.GwClosed(gwId, scopeId);
    var consumedKey = GatewayScopeKeys.GwConsumed(gwId, scopeId);

    // Expected executable branches (scope-only) - MUST be written by split
    var expectedExecKey = GatewayScopeKeys.ScopeExpectedExec(scopeId);

    // If already closed => late arrivals are consumed
    if (GetBoolVar(process, closedKey))
    {
        token.Terminate("Late arrival to closed join/merge.");
        IncIntVar(process, consumedKey, 1);
        await _uow.Tokens.UpdateAsync(token, ct);
        await _uow.Processes.UpdateAsync(process, ct);
        return TokenProcessResult.Consumed;
    }

    // ------------------------------------------------------------
    // XOR MERGE (Exclusive): first token creates merged token, others terminated
    // ------------------------------------------------------------
    if (gateway is BpmnExclusiveGateway)
    {
        // Check if merge already completed (merged token exists)
        var xorMergedTokenKey = GatewayScopeKeys.GwMergedToken(gwId, scopeId);
        var xorExistingMergedTokenId = GetGuidVar(process, xorMergedTokenKey);
        
        if (xorExistingMergedTokenId is not null)
        {
            // Late arrival to closed XOR merge
            token.Terminate("Late arrival to closed XOR merge.");
            IncIntVar(process, consumedKey, 1);
            await _uow.Tokens.UpdateAsync(token, ct);
            await _uow.Processes.UpdateAsync(process, ct);
            return TokenProcessResult.Consumed;
        }

        // First token to arrive: terminate it and create merged token
        // ✅ Complete node for executable token when merged
        if (token.IsExecutable)
        {
            var tokenNodes = await _nodeRepository.GetByTokenIdAsync(token.Id, ct);
            foreach (var node in tokenNodes)
            {
                // Only complete nodes that are waiting or processing (not already completed/failed)
                if (node.State == NodeState.Waiting || node.State == NodeState.Processing || node.State == NodeState.Created)
                {
                    node.Complete();
                    await _nodeRepository.UpdateAsync(node, ct);
                    _logger.LogInformation(
                        "[JOIN] Completed node for XOR merged executable token. NodeId={NodeId} TokenId={TokenId} ElementId={ElementId}",
                        node.Id, token.Id, node.ElementId);
                }
            }
        }
        
        token.Terminate("Merged at XOR merge gateway.");
        IncIntVar(process, consumedKey, 1);
        
        SetVar(process, closedKey, true);
        
        // ✅ CRITICAL: Save token termination and process changes
        await _uow.Tokens.UpdateAsync(token, ct);
        await _uow.Processes.UpdateAsync(process, ct);

        // Get outgoing flow for merged token
        var xorOutgoing = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, gwId);
        if (xorOutgoing.Count == 0)
        {
            token.Fail($"XOR merge gateway '{gwId}' has no outgoing sequence flows.");
            return TokenProcessResult.Failed;
        }

        var xorNextFlow = xorOutgoing[0];
        if (string.IsNullOrWhiteSpace(xorNextFlow.targetRef))
        {
            token.Fail($"XOR merge gateway '{gwId}' outgoing flow has no targetRef.");
            return TokenProcessResult.Failed;
        }

        // Create merged token via command
        // Collect flow IDs ONLY from executable token and add the outgoing flow
        var xorFlowIds = new List<string>();
        if (!string.IsNullOrWhiteSpace(xorNextFlow.id))
        {
            xorFlowIds.Add(xorNextFlow.id);
        }
        // Only collect flow IDs from executable tokens (non-executable tokens don't create nodes)
        if (token.IsExecutable)
        {
            foreach (var flowId in token.ArrivedViaFlowIds)
            {
                if (!string.IsNullOrWhiteSpace(flowId) && !xorFlowIds.Contains(flowId, StringComparer.Ordinal))
                {
                    xorFlowIds.Add(flowId);
                }
            }
        }
        
        var xorMergedTokenId = await _mediator.Send(new CreateMergedTokenCommand(
            ProcessId: process.Id,
            GatewayId: gwId,
            ScopeId: scopeId,
            ParentTokenIds: new[] { token.Id }
        ), ct);

        _logger.LogInformation(
            "[JOIN] XOR merge: merged token created. Gw={Gw} Scope={Scope} MergedTokenId={MergedTokenId} ParentTokenId={ParentTokenId}",
            gwId, scopeId, xorMergedTokenId, token.Id);

        return TokenProcessResult.Consumed;
    }

    // ------------------------------------------------------------
    // AND/OR JOIN (Parallel/Inclusive): wait until expectedExec arrives
    // ------------------------------------------------------------
    var expectedExec = GetIntVar(process, expectedExecKey, defaultValue: 0);
    if (expectedExec <= 0)
    {
        // FAIL-FAST: continuing here causes premature join (the bug you saw)
        _logger.LogError(
            "[JOIN] expectedExec missing/invalid. Gw={Gw} Scope={Scope} TokenId={TokenId}",
            gwId, scopeId, token.Id);
        token.Fail($"Join expectedExec missing/invalid. Gw={gwId} Scope={scopeId}");
        return TokenProcessResult.Failed;
    }

    // Check if merge already completed (idempotency check)
    var mergedTokenKey = GatewayScopeKeys.GwMergedToken(gwId, scopeId);
    var existingMergedTokenId = GetGuidVar(process, mergedTokenKey);
    
    if (existingMergedTokenId is not null)
    {
        // Late arrival to closed join
        var terminateReason = token.IsExecutable 
            ? "Late arrival to closed join/merge." 
            : "Trace token: late arrival to closed join/merge.";
        token.Terminate(terminateReason);
        IncIntVar(process, consumedKey, 1);
        await _uow.Tokens.UpdateAsync(token, ct);
        await _uow.Processes.UpdateAsync(process, ct);
        return TokenProcessResult.Consumed;
    }

    // ✅ CRITICAL: Use gwId (not token.CurrentElementId) for counting arrived tokens
    // Count arrived EXECUTABLE tokens at THIS gateway in THIS scope (Active+Waiting)
    var arrivedExecutable = await _tokenRepository.CountArrivedAtAsync(
        processId: process.Id,
        elementId: gwId,  // ✅ Use gateway.id, not token.CurrentElementId
        scopeId: scopeId,
        executableOnly: true,
        ct: ct);

    // ✅ Load-bearing log: helps diagnose if join is waiting or satisfied
    _logger.LogInformation(
        "[JOIN] Gw={Gw} Scope={Scope} expectedExec={Expected} arrivedExecutable={ArrivedExec} TokenId={TokenId} IsExecutable={IsExec}",
        gwId, scopeId, expectedExec, arrivedExecutable, token.Id, token.IsExecutable);

    if (arrivedExecutable < expectedExec)
    {
        // Join not satisfied yet: wait if executable, terminate if non-executable (trace)
        if (!token.IsExecutable)
        {
            // Non-executable tokens don't wait - they are terminated immediately
            token.Terminate("Trace token merged at join gateway.");
            IncIntVar(process, consumedKey, 1);
            await _uow.Tokens.UpdateAsync(token, ct);
            await _uow.Processes.UpdateAsync(process, ct);
            return TokenProcessResult.Consumed;
        }

        _logger.LogInformation(
            "[JOIN] Waiting. Gw={Gw} Scope={Scope} arrivedExec={ArrivedExec} expected={Expected} TokenId={TokenId}",
            gwId, scopeId, arrivedExecutable, expectedExec, token.Id);
        token.Wait($"Join waiting: arrivedExecutable={arrivedExecutable}, expected={expectedExec}");
        await _uow.Tokens.UpdateAsync(token, ct);
        return TokenProcessResult.Waiting;
    }

    // ------------------------------------------------------------
    // JOIN SATISFIED:
    // - Terminate ALL arrived tokens (executable and non-executable)
    // - Create new merged token via command
    // ------------------------------------------------------------

    // Load all arrived executable tokens (must include Active + Waiting)
    // ✅ CRITICAL: Use gwId (not token.CurrentElementId) for loading arrived tokens
    var arrivedExecutableTokens = await _tokenRepository.GetArrivedAtAsync(
        processId: process.Id,
        elementId: gwId,  // ✅ Use gateway.id, not token.CurrentElementId
        scopeId: scopeId,
        executableOnly: true,
        ct: ct);

    // Also load non-executable tokens at this gateway/scope to terminate them
    var allArrivedTokens = await _tokenRepository.GetArrivedAtAsync(
        processId: process.Id,
        elementId: gwId,
        scopeId: scopeId,
        executableOnly: false,
        ct: ct);

    if (arrivedExecutableTokens.Count == 0)
    {
        token.Fail($"Join satisfied but no executable arrived tokens loaded. Gw={gwId} Scope={scopeId}");
        return TokenProcessResult.Failed;
    }

    // ✅ CRITICAL: Mark join as closed BEFORE terminating tokens and creating merged token
    // This prevents late arrivals from triggering another merge attempt
    // Must be set in the same transaction as token termination and merged token creation
    SetVar(process, closedKey, true);

    // ✅ Policy: Terminate ALL arrived tokens (executable and non-executable)
    // No winner concept - all tokens are consumed by the merge
    // ✅ IMPORTANT: Complete nodes for executable tokens, skip nodes for non-executable tokens
    var parentTokenIds = new List<Guid>(arrivedExecutableTokens.Count);
    
    for (var i = 0; i < allArrivedTokens.Count; i++)
    {
        var t = allArrivedTokens[i];

        if (t.State is TokenState.Terminated or TokenState.Failed)
            continue;

        // Collect parent token IDs (only executable tokens for parent tracking)
        if (t.IsExecutable)
        {
            parentTokenIds.Add(t.Id);
            
            // ✅ Complete nodes for executable tokens when merged
            var tokenNodes = await _nodeRepository.GetByTokenIdAsync(t.Id, ct);
            foreach (var node in tokenNodes)
            {
                // Only complete nodes that are waiting or processing (not already completed/failed)
                if (node.State == NodeState.Waiting || node.State == NodeState.Processing || node.State == NodeState.Created)
                {
                    node.Complete();
                    await _nodeRepository.UpdateAsync(node, ct);
                    _logger.LogInformation(
                        "[JOIN] Completed node for merged executable token. NodeId={NodeId} TokenId={TokenId} ElementId={ElementId}",
                        node.Id, t.Id, node.ElementId);
                }
            }
            
            t.Terminate("Merged at join gateway - executable token consumed.");
        }
        else
        {
            // Non-executable tokens shouldn't have nodes, but if they do, skip them
            t.Terminate("Trace merged at join gateway.");
        }
        
        // ✅ CRITICAL: Save token termination to database
        await _uow.Tokens.UpdateAsync(t, ct);
        
        IncIntVar(process, consumedKey, 1);
    }
    
    // ✅ CRITICAL: Save process variable changes (including closedKey) BEFORE creating merged token
    // This ensures that if another token arrives concurrently, it will see closedKey=true
    // and will be terminated as a late arrival instead of triggering another merge
    await _uow.Processes.UpdateAsync(process, ct);

    // ✅ Create new merged token via Command (with idempotency support)
    // The command handler will check again for existing merged token inside its transaction
    var joinOutgoing = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, gwId);
    if (joinOutgoing.Count == 0)
    {
        token.Fail($"Join gateway '{gwId}' has no outgoing sequence flows.");
        return TokenProcessResult.Failed;
    }

    // Get the single outgoing flow (join has exactly one outgoing)
    var nextFlow = joinOutgoing[0];
    if (string.IsNullOrWhiteSpace(nextFlow.targetRef))
    {
        token.Fail($"Join gateway '{gwId}' outgoing flow has no targetRef.");
        return TokenProcessResult.Failed;
    }

    
    var mergedTokenId = await _mediator.Send(new CreateMergedTokenCommand(
        ProcessId: process.Id,
        GatewayId: gwId,
        ScopeId: scopeId,
        ParentTokenIds: parentTokenIds
    ), ct);

    _logger.LogInformation(
        "[JOIN] Merged token created and activated. Gw={Gw} Scope={Scope} MergedTokenId={MergedTokenId} ParentCount={ParentCount} NextElement={NextElement}",
        gwId, scopeId, mergedTokenId, parentTokenIds.Count, nextFlow.targetRef);

    // Current token is now terminated (if it was in arrivedTokens)
    // Return Consumed for all tokens that participated in join
    return TokenProcessResult.Consumed;
}


    // ============================================================
    // NODE PROCESS: Safe (NO join logic)
    // - IO mapping (inputs/outputs)
    // - token.Processed()
    // - node.Complete/Fail/Wait derived from token state
    // ============================================================
    public override Task<ElementProcessResult> NodeProcessAsync(
        Process process,
        Token token,
        NodeInstance node,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        // If token already terminal or waiting, node should follow it.
        if (token.State is TokenState.Terminated)
        {
            node.Complete(); // or node.Terminate(...) if you have it
            return Task.FromResult(ElementProcessResult.Terminated);
        }

        if (token.State is TokenState.Failed)
        {
            node.Fail("Token failed before node processing.");
            return Task.FromResult(ElementProcessResult.Failed);
        }

        if (token.State is TokenState.Waiting)
        {
            // join waiting or other pause
            // (adjust signature if your NodeInstance.Wait() differs)
            node.WaitForJoin();
            return Task.FromResult(ElementProcessResult.Waiting);
        }

        var outgoing = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);

        if (outgoing.Count == 0)
        {
            Logger.LogWarning("[GW] No outgoing flows. ElementId={ElementId}", token.CurrentElementId);
            node.Fail("Gateway has no outgoing sequence flows.");
            token.Fail("Gateway has no outgoing sequence flows.");
            return Task.FromResult(ElementProcessResult.Failed);
        }

        // ⛔ Gateway does NOT perform variable mapping (only state/control + join variables)
        // Mark processed + complete node
        token.Processed();
        node.Complete();

        return Task.FromResult(ElementProcessResult.Completed);
    }

// ============================================================
    // TOKEN NAVIGATION: Split/Fork or default route
    // ============================================================
    public override async Task TokenNavigateAsync(
        Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        if (token.State is TokenState.Waiting or TokenState.Terminated or TokenState.Failed)
            return;

        var gateway = (BpmnGateway)element;
        var outgoing = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);
        if (outgoing is null || outgoing.Count == 0)
            return;

        // Split/Fork only here
        if (outgoing.Count > 1)
        {
            var splitHandled = await _split.TrySplitAsync(process, token, gateway, ctx, ct);
            if (splitHandled)
                return;

            _logger.LogWarning(
                "[GW:NAV] Split expected but not handled. Falling back to default navigation. Gw={Gw}",
                gateway.id);
        }

        // Fallback to base navigation (single outgoing OR split not handled)
        await base.TokenNavigateAsync(process, token, element, ctx, isResume, ct);
    }

    private static int GetIntVar(Process process, string key, int defaultValue = 0)
    {
        if (!process.Variables.TryGetValue(key, out var v) || v is null) return defaultValue;
        try { return Convert.ToInt32(v); } catch { return defaultValue; }
    }

    private static bool GetBoolVar(Process process, string key, bool defaultValue = false)
    {
        if (!process.Variables.TryGetValue(key, out var raw) || raw is null)
            return defaultValue;

        // normalize to string (handles Dictionary<string,string> or Dictionary<string,object>)
        var s = raw as string ?? raw.ToString();
        if (string.IsNullOrWhiteSpace(s))
            return defaultValue;

        // accept: "true/false", "1/0", "yes/no"
        if (bool.TryParse(s, out var b))
            return b;

        if (int.TryParse(s, out var i))
            return i != 0;

        if (string.Equals(s, "yes", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(s, "no", StringComparison.OrdinalIgnoreCase))
            return false;

        return defaultValue;
    }

    private static Guid? GetGuidVar(Process process, string key)
    {
        if (!process.Variables.TryGetValue(key, out var raw) || raw is null)
            return null;

        var s = raw as string ?? raw.ToString();
        if (string.IsNullOrWhiteSpace(s))
            return null;

        return Guid.TryParse(s, out var g) ? g : null;
    }

    private static void SetVar(Process process, string key, object? value)
        => process.SetVariable(key, value?.ToString() ?? string.Empty);

    private static void IncIntVar(Process process, string key, int delta = 1)
    {
        var cur = GetIntVar(process, key, 0);
        SetVar(process, key, (cur + delta).ToString());
    }
}