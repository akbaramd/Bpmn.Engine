using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Gateway routing service (Zeebe-inspired structured execution):
/// - Only forks when > 1 branches are actually taken.
/// - XOR/OR with a single taken flow routes the SAME token (no child tokens).
/// - AND forks all outgoing.
/// - OR forks chosenMany (>1) else routes single/default.
/// - EventBasedGateway is not handled here (subscription-based).
///
/// IMPORTANT (Zeebe-like):
/// - When forking, we create a NEW scopeId and PUSH it on parent token.
/// - Children MUST receive the SAME scope snapshot (parent.ScopeStack) and ParentTokenId=parent.Id.
///   (This is responsibility of ITokenForkService / CreateTokenCommand.)
/// - ExpectedCount is stored in Process.Metadata keyed by scopeId.
/// </summary>
public sealed class GatewaySplitService : IGatewaySplitService
{
    private readonly ITokenForkService _fork;
    private readonly ISequenceFlowSelector _selector;
    private readonly ILogger<GatewaySplitService> _logger;

    public GatewaySplitService(
        ITokenForkService fork,
        ISequenceFlowSelector selector,
        ILogger<GatewaySplitService> logger)
    {
        _fork = fork ?? throw new ArgumentNullException(nameof(fork));
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> TrySplitAsync(
        Process process,
        Token token,
        BpmnGateway gateway,
        BpmnRuntimeContext ctx,
        CancellationToken ct)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (token is null) throw new ArgumentNullException(nameof(token));
        if (gateway is null) throw new ArgumentNullException(nameof(gateway));
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        var gwId = gateway.id ?? throw new InvalidOperationException("Gateway must have id.");

        // Only split candidates when outgoing > 1
        var outgoingRaw = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, gwId);
        if (outgoingRaw is null || outgoingRaw.Count <= 1)
            return false;

        // Must be Active (if not, ignore but mark handled to avoid retry storms)
        if (token.State != TokenState.Active)
        {
            _logger.LogDebug(
                "[SPLIT:SKIP] State!=Active. Gw={Gw} Type={Type} Token={Token} State={State} Scope={Scope}",
                gwId, gateway.GetType().Name, token.Id, token.State, token.ScopeId);
            return true;
        }

        // Event-based must be implemented with subscriptions
        if (gateway is BpmnEventBasedGateway)
        {
            token.Fail($"EventBasedGateway '{gwId}' is not supported here (requires subscriptions).");
            _logger.LogError("[SPLIT:FAIL] EventBasedGateway not supported. Gw={Gw} Token={Token}", gwId, token.Id);
            return true;
        }

        // Validate + deduplicate outgoing
        var outgoingAll = DeduplicateAndValidate(outgoingRaw, gateway, token);
        if (token.State == TokenState.Failed)
            return true;

        if (outgoingAll.Count <= 1)
            return false;

        // Choose flows according to gateway semantics
        var flowsChosen = DetermineFlows(process, token, gateway, outgoingAll);

        if (token.State == TokenState.Failed)
            return true;

        if (flowsChosen.Count == 0)
        {
            token.Fail($"Gateway '{gwId}' produced 0 outgoing flows (no condition matched and no default).");
            _logger.LogError("[SPLIT:FAIL] 0 flows chosen. Gw={Gw} Token={Token}", gwId, token.Id);
            return true;
        }

        // ✅ If only 1 flow => route SAME token (no fork)
        if (flowsChosen.Count == 1)
        {
            var f = flowsChosen[0];
            if (string.IsNullOrWhiteSpace(f.targetRef))
            {
                token.Fail($"Gateway '{gwId}' has chosen flow with empty targetRef. Flow={FlowKey(f)}");
                _logger.LogError("[SPLIT:FAIL] Empty targetRef. Gw={Gw} Token={Token} Flow={Flow}", gwId, token.Id, FlowKey(f));
                return true;
            }

            _logger.LogInformation(
                "[ROUTE] Gw={Gw} Type={Type} Token={Token} Flow={Flow} Target={Target} Scope={Scope}",
                gwId, gateway.GetType().Name, token.Id, FlowKey(f), f.targetRef, token.ScopeId);

            token.MoveTo(f.targetRef!, skipProcess: false, FlowKey(f));
            return true;
        }

        // ✅ Real fork (2+ branches)
        var scopeId = Guid.NewGuid();

        _logger.LogInformation(
            "[FORK] Gw={Gw} Type={Type} Token={Token} NewScope={Scope} OutgoingTotal={Total} ForkCount={ForkCount} PrevScope={PrevScope}",
            gwId, gateway.GetType().Name, token.Id, scopeId, outgoingAll.Count, flowsChosen.Count, token.ScopeId);

        // Persist join expectations (scope-correlated)
        PersistJoinExpectations(process, scopeId, gateway, flowsChosen);

        // Park parent waiting for join:
        // Zeebe-like: PUSH scope on parent
        token.SetScope(scopeId); // alias for PushScope(scopeId)
        token.Fork(flowsChosen.Count, $"Gateway '{gwId}' forked {flowsChosen.Count} branch token(s).");

        // Fork children:
        // IMPORTANT: ForkChildrenAsync MUST create child tokens with:
        // - ParentTokenId = token.Id
        // - ScopeStackSnapshot = token.ScopeStack (includes scopeId on top)
        // - ArrivedViaFlowId = each chosen flow key
        await _fork.ForkChildrenAsync(
            process: process,
            parent: token,
            outgoing: flowsChosen,
            scopeId: scopeId,
            ctx: ctx,
            ct: ct);

        return true;
    }

    // -------------------- Flow selection rules --------------------

    private List<BpmnSequenceFlow> DetermineFlows(
        Process process,
        Token token,
        BpmnGateway gateway,
        List<BpmnSequenceFlow> outgoingAll)
    {
        // AND split: all branches
        if (gateway is BpmnParallelGateway)
            return outgoingAll;

        // XOR split: exactly one branch
        if (gateway is BpmnExclusiveGateway)
        {
            var defaultId = GetGatewayDefaultFlowId(gateway);

            var chosen =
                _selector.ChooseOne(outgoingAll, gateway, process, token)
                ?? ResolveDefaultFlow(outgoingAll, defaultId)
                ?? outgoingAll[0];

            return new List<BpmnSequenceFlow>(1) { chosen };
        }

        // OR split: one-or-more branches
        if (gateway is BpmnInclusiveGateway)
        {
            var defaultId = GetGatewayDefaultFlowId(gateway);
            var hasAnyCondition = HasAnyCondition(outgoingAll);

            // If no conditions exist => ALL flows (common BPMN usage)
            if (!hasAnyCondition)
            {
                _logger.LogWarning("[SPLIT] OR: no conditions => ALL flows. Gw={Gw}", gateway.id);
                return outgoingAll;
            }

            var chosenMany = _selector.ChooseMany(outgoingAll, gateway, process, token);

            if (chosenMany is null || chosenMany.Count == 0)
            {
                var def = ResolveDefaultFlow(outgoingAll, defaultId);
                return def is null ? new List<BpmnSequenceFlow>(0) : new List<BpmnSequenceFlow>(1) { def };
            }

            // Deduplicate chosenMany safely
            var dedup = new Dictionary<string, BpmnSequenceFlow>(StringComparer.Ordinal);
            foreach (var f in chosenMany)
            {
                var k = FlowKey(f);
                if (!dedup.ContainsKey(k))
                    dedup[k] = f;
            }

            var list = new List<BpmnSequenceFlow>(dedup.Count);
            foreach (var kv in dedup)
                list.Add(kv.Value);

            return list;
        }

        token.Fail($"Unsupported gateway type '{gateway.GetType().Name}' at '{gateway.id}'.");
        _logger.LogError("[SPLIT:FAIL] Unsupported gateway. Token={Token} Gw={Gw} Type={Type}",
            token.Id, gateway.id, gateway.GetType().Name);

        return new List<BpmnSequenceFlow>(0);
    }

    // -------------------- Join expectation persistence --------------------

    private void PersistJoinExpectations(
        Process process,
        Guid scopeId,
        BpmnGateway gateway,
        List<BpmnSequenceFlow> chosen)
    {
        process.SetMetadata(JoinCorrelationMetaKeys.SplitGatewayId(scopeId), gateway.id);
        process.SetMetadata(JoinCorrelationMetaKeys.SplitGatewayType(scopeId), gateway.GetType().Name);
        process.SetMetadata(JoinCorrelationMetaKeys.ExpectedCount(scopeId), chosen.Count.ToString());

        var sb = new System.Text.StringBuilder(capacity: chosen.Count * 32);
        for (var i = 0; i < chosen.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(!string.IsNullOrWhiteSpace(chosen[i].id)
                ? chosen[i].id
                : $"{chosen[i].sourceRef}->{chosen[i].targetRef}");
        }

        process.SetMetadata(JoinCorrelationMetaKeys.Branches(scopeId), sb.ToString());

        _logger.LogDebug(
            "[FORK:CFG] Stored join expectations. Scope={Scope} Expected={Expected} SplitGw={Gw} Branches={Branches}",
            scopeId, chosen.Count, gateway.id, sb.ToString());
    }

    // -------------------- helpers --------------------

    private static List<BpmnSequenceFlow> DeduplicateAndValidate(
        List<BpmnSequenceFlow> outgoingRaw,
        BpmnGateway gateway,
        Token token)
    {
        var dict = new Dictionary<string, BpmnSequenceFlow>(outgoingRaw.Count, StringComparer.Ordinal);

        for (var i = 0; i < outgoingRaw.Count; i++)
        {
            var f = outgoingRaw[i];

            if (string.IsNullOrWhiteSpace(f.targetRef))
            {
                token.Fail($"Split gateway '{gateway.id}' has outgoing flow(s) with empty targetRef. FlowIndex={i}");
                return new List<BpmnSequenceFlow>(0);
            }

            var key = FlowKeyStable(f, i);
            if (!dict.ContainsKey(key))
                dict[key] = f;
        }

        var list = new List<BpmnSequenceFlow>(dict.Count);
        foreach (var kv in dict)
            list.Add(kv.Value);

        return list;
    }

    private static bool HasAnyCondition(List<BpmnSequenceFlow> flows)
    {
        for (var i = 0; i < flows.Count; i++)
        {
            var c = GetConditionText(flows[i]);
            if (!string.IsNullOrWhiteSpace(c)) return true;
        }
        return false;
    }

    private static string? GetConditionText(BpmnSequenceFlow f)
    {
        var ce = f.conditionExpression;
        if (ce?.Text == null || ce.Text.Length == 0) return null;

        if (ce.Text.Length == 1)
        {
            var s = ce.Text[0]?.Trim();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        var combined = string.Concat(ce.Text).Trim();
        return string.IsNullOrWhiteSpace(combined) ? null : combined;
    }

    private static string? GetGatewayDefaultFlowId(BpmnGateway gateway)
    {
        var gt = gateway.GetType();
        return gt.GetProperty("default")?.GetValue(gateway) as string
               ?? gt.GetProperty("Default")?.GetValue(gateway) as string;
    }

    private static BpmnSequenceFlow? ResolveDefaultFlow(IReadOnlyList<BpmnSequenceFlow> outgoing, string? defaultFlowId)
    {
        if (string.IsNullOrWhiteSpace(defaultFlowId)) return null;

        for (var i = 0; i < outgoing.Count; i++)
        {
            var f = outgoing[i];
            if (!string.IsNullOrWhiteSpace(f.id) &&
                string.Equals(f.id, defaultFlowId, StringComparison.Ordinal))
                return f;

            if (string.Equals(FlowKey(f), defaultFlowId, StringComparison.Ordinal))
                return f;
        }

        return null;
    }

    private static string FlowKey(BpmnSequenceFlow f)
        => !string.IsNullOrWhiteSpace(f.id) ? f.id! : $"{f.sourceRef}->{f.targetRef}";

    private static string FlowKeyStable(BpmnSequenceFlow f, int indexWhenNoId)
        => !string.IsNullOrWhiteSpace(f.id)
            ? f.id!
            : $"{f.sourceRef}->{f.targetRef}#{indexWhenNoId}";
}
