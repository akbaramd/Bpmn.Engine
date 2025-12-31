using System.Linq;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<GatewayHandler> _logger;

    
    public GatewayHandler(
        IGatewaySplitService split,
        IVariableMappingService variableMapping,
        IFeelExpressionEvaluator feel,
        ILogger<GatewayHandler> logger,
        ITokenRepository tokenRepository,
        INodeInstanceRepository nodeRepository,
        IUnitOfWork uow)
        : base(feel, logger)
    {
        _split = split ?? throw new ArgumentNullException(nameof(split));
        _variableMapping = variableMapping ?? throw new ArgumentNullException(nameof(variableMapping));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenRepository = tokenRepository ?? throw new ArgumentNullException(nameof(tokenRepository));
        _nodeRepository = nodeRepository ?? throw new ArgumentNullException(nameof(nodeRepository));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
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
    if (token.State is TokenState.Terminated or TokenState.Failed or TokenState.Merged)
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

    // ✅ Find parent token - all children should have the same parent
    if (token.ParentTokenIds.Count == 0)
    {
        _logger.LogError(
            "[JOIN] Token has no parent token. Gw={Gw} TokenId={TokenId} Scope={Scope}",
            gwId, token.Id, scopeId);
        token.Fail($"Join gateway '{gwId}' token has no parent token.");
        return TokenProcessResult.Failed;
    }

    // All children should have the same parent (first parent is used)
    var parentTokenId = token.ParentTokenIds.First();
    var parentToken = await _tokenRepository.GetByIdAsync(parentTokenId, ct);
    
    if (parentToken == null)
    {
        _logger.LogError(
            "[JOIN] Parent token not found. Gw={Gw} TokenId={TokenId} ParentTokenId={ParentTokenId} Scope={Scope}",
            gwId, token.Id, parentTokenId, scopeId);
        token.Fail($"Join gateway '{gwId}' parent token not found. ParentTokenId={parentTokenId}");
        return TokenProcessResult.Failed;
    }

    if (parentToken.State != TokenState.Forked)
    {
        _logger.LogWarning(
            "[JOIN] Parent token is not in Forked state. Gw={Gw} TokenId={TokenId} ParentTokenId={ParentTokenId} ParentState={ParentState} Scope={Scope}",
            gwId, token.Id, parentTokenId, parentToken.State, scopeId);
        // Continue anyway - might be a race condition
    }

    // ✅ Mark current token as Merged
    // Complete node for executable token when merged
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
                    "[JOIN] Completed node for merged executable token. NodeId={NodeId} TokenId={TokenId} ElementId={ElementId}",
                    node.Id, token.Id, node.ElementId);
            }
        }
    }

    token.Merge(parentTokenId, $"Merged at join gateway '{gwId}'.");
    await _uow.Tokens.UpdateAsync(token, ct);

    // ✅ Count merged tokens for the same parent at this gateway/scope
    // Get all child tokens of the parent and count those that are at this gateway and merged
    var childTokens = await _tokenRepository.GetChildTokensAsync(parentTokenId, ct);
    var mergedCount = childTokens.Count(t => 
        t.ProcessId == process.Id &&
        t.CurrentElementId == gwId &&
        t.ScopeId == scopeId &&
        t.State == TokenState.Merged);

    _logger.LogInformation(
        "[JOIN] Token merged. Gw={Gw} Scope={Scope} TokenId={TokenId} ParentTokenId={ParentTokenId} MergedCount={MergedCount} IncomingCount={IncomingCount}",
        gwId, scopeId, token.Id, parentTokenId, mergedCount, incoming.Count);

    // ------------------------------------------------------------
    // XOR MERGE (Exclusive): first token reactivates parent
    // ------------------------------------------------------------
    if (gateway is BpmnExclusiveGateway)
    {
        // XOR merge: only one token should arrive, so reactivate parent immediately
        if (mergedCount >= 1)
        {
            // Reactivate parent token
            parentToken.ReactivateFromForked(mergedCount, $"XOR merge completed at gateway '{gwId}'.");
            await _uow.Tokens.UpdateAsync(parentToken, ct);

            // Move parent to next element
            var nextFlow = outgoing[0];
            if (string.IsNullOrWhiteSpace(nextFlow.targetRef))
            {
                parentToken.Fail($"XOR merge gateway '{gwId}' outgoing flow has no targetRef.");
                await _uow.Tokens.UpdateAsync(parentToken, ct);
                return TokenProcessResult.Failed;
            }

            parentToken.MoveTo(nextFlow.targetRef, nextFlow.id);
            await _uow.Tokens.UpdateAsync(parentToken, ct);

            _logger.LogInformation(
                "[JOIN] XOR merge: parent reactivated. Gw={Gw} Scope={Scope} ParentTokenId={ParentTokenId} NextElement={NextElement}",
                gwId, scopeId, parentTokenId, nextFlow.targetRef);

            return TokenProcessResult.Consumed;
        }
    }

    // ------------------------------------------------------------
    // AND/OR JOIN (Parallel/Inclusive): wait until all children merged
    // ------------------------------------------------------------
    // Check if all children have merged (mergedCount == incoming flows count)
    if (mergedCount >= incoming.Count)
    {
        // All children have merged - reactivate parent token
        parentToken.ReactivateFromForked(mergedCount, $"Join completed at gateway '{gwId}' - all {mergedCount} children merged.");
        await _uow.Tokens.UpdateAsync(parentToken, ct);

        // Move parent to next element
        var nextFlow = outgoing[0];
        if (string.IsNullOrWhiteSpace(nextFlow.targetRef))
        {
            parentToken.Fail($"Join gateway '{gwId}' outgoing flow has no targetRef.");
            await _uow.Tokens.UpdateAsync(parentToken, ct);
            return TokenProcessResult.Failed;
        }

        parentToken.MoveTo(nextFlow.targetRef, nextFlow.id);
        await _uow.Tokens.UpdateAsync(parentToken, ct);

        _logger.LogInformation(
            "[JOIN] All children merged: parent reactivated. Gw={Gw} Scope={Scope} ParentTokenId={ParentTokenId} MergedCount={MergedCount} NextElement={NextElement}",
            gwId, scopeId, parentTokenId, mergedCount, nextFlow.targetRef);

        return TokenProcessResult.Consumed;
    }

    // Not all children have merged yet - wait for more
    _logger.LogInformation(
        "[JOIN] Waiting for more children. Gw={Gw} Scope={Scope} MergedCount={MergedCount} IncomingCount={IncomingCount}",
        gwId, scopeId, mergedCount, incoming.Count);

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