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
/// Gateway split/fork service (production-ready).
///
/// Responsibilities:
/// - Split only when outgoing > 1 (fork candidate)
/// - Determine executability for each outgoing flow (AND/XOR/OR)
/// - Create ScopeId for correlation at join
/// - Persist expected counts on Process variables (by ScopeId)
/// - Consume parent token and fork children via ITokenForkService
///
/// Policies:
/// - Non-executable tokens MUST NOT fork (they must use default navigation)
/// - EventBasedGateway is NOT handled here (must be subscription-based) => fail fast
/// - Structural issues (missing targetRef) => fail token and mark split as handled
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

        // Split candidates only (outgoing > 1)
        var outgoingRaw = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);
        if (outgoingRaw is null || outgoingRaw.Count <= 1)
            return false;

        // Must be Active
        if (token.State != TokenState.Active)
        {
            _logger.LogWarning(
                "[SPLIT] Ignored (state != Active). State={State} TokenId={TokenId} Gw={Gw}",
                token.State, token.Id, gateway.id);
            return true; // handled: do not retry/fallback
        }

        // Policy: trace/non-executable tokens never fork
        if (!token.IsExecutable)
        {
            _logger.LogDebug("[SPLIT] Skipped (non-executable). TokenId={TokenId} Gw={Gw}", token.Id, gateway.id);
            return false; // allow default navigation
        }

        // Event-based must be implemented with subscriptions, not split-service
        if (gateway is BpmnEventBasedGateway)
        {
            token.Fail($"EventBasedGateway '{gateway.id}' is not supported by GatewaySplitService (requires subscriptions).");
            _logger.LogError("[SPLIT] EventBasedGateway not supported. TokenId={TokenId} Gw={Gw}", token.Id, gateway.id);
            return true; // handled by failing token
        }

        // Remove duplicates + structural validation
        var outgoingAll = DeduplicateAndValidate(outgoingRaw, gateway, token);
        if (token.State == TokenState.Failed)
            return true; // handled by failing token

        if (outgoingAll.Count <= 1)
            return false;

        // Decide executability
        var (execPredicate, expectedExec) = BuildExecutability(process, token, gateway, outgoingAll);

        // Normalize expectedExec
        if (expectedExec < 0) expectedExec = 0;
        if (expectedExec > outgoingAll.Count) expectedExec = outgoingAll.Count;

        // Strict safety: executable parent must create at least one executable branch.
        // Otherwise the process would dead-end or cause surprising behavior.
        if (expectedExec == 0)
        {
            token.Fail($"Gateway '{gateway.id}' produced 0 executable outgoing flows (no condition matched and no default).");
            _logger.LogError("[SPLIT] 0 executable branches. TokenId={TokenId} Gw={Gw}", token.Id, gateway.id);
            return true;
        }

        var scopeId = Guid.NewGuid();

        // Persist expected counts (MUST match join logic keys)
        process.SetVariable(GatewayScopeKeys.ScopeExpectedTotal(scopeId), outgoingAll.Count.ToString());
        process.SetVariable(GatewayScopeKeys.ScopeExpectedExec(scopeId), expectedExec.ToString());

        _logger.LogInformation(
            "[SPLIT] Gw={Gw} Type={Type} TokenId={TokenId} ScopeId={ScopeId} Total={Total} ExecExpected={ExecExpected}",
            gateway.id, gateway.GetType().Name, token.Id, scopeId, outgoingAll.Count, expectedExec);

        // ✅ Policy: Parent token is Terminated (not Completed) - it was consumed by split
        // This ensures it doesn't count toward process completion
        token.Terminate($"Split gateway '{gateway.id}' forked {outgoingAll.Count} branch token(s).");
        process.RemoveToken(token.Id);

        // Fork children: MUST set child.ScopeId = scopeId and set IsExecutable per flow
        await _fork.ForkChildrenAsync(
            process: process,
            parent: token,
            outgoing: outgoingAll,
            scopeId: scopeId,
            isExecutableForFlow: execPredicate,
            ctx: ctx,
            ct: ct);

        return true;
    }

    // -------------------- Executability rules --------------------

    private (Func<BpmnSequenceFlow, bool> predicate, int expectedExec) BuildExecutability(
        Process process,
        Token token,
        BpmnGateway gateway,
        List<BpmnSequenceFlow> outgoingAll)
    {
        // AND split: all branches executable
        if (gateway is BpmnParallelGateway)
            return (static _ => true, expectedExec: outgoingAll.Count);

        // XOR split: exactly one branch executable
        if (gateway is BpmnExclusiveGateway)
        {
            var defaultId = GetGatewayDefaultFlowId(gateway);

            var chosen =
                _selector.ChooseOne(outgoingAll, gateway, process, token)
                ?? ResolveDefaultFlow(outgoingAll, defaultId)
                ?? outgoingAll[0]; // deterministic fallback

            var chosenKey = FlowKey(chosen);
            return (f => StringComparer.OrdinalIgnoreCase.Equals(FlowKey(f), chosenKey), expectedExec: 1);
        }

        // OR split: one-or-more branches executable
        if (gateway is BpmnInclusiveGateway)
        {
            var defaultId = GetGatewayDefaultFlowId(gateway);
            var hasAnyCondition = HasAnyCondition(outgoingAll);

            // If no conditions exist at all, treat as all executable (common interpretation).
            // NOTE: If you want stricter BPMN validation, replace with "default-only or fail".
            if (!hasAnyCondition)
            {
                _logger.LogWarning("[SPLIT] OR: no conditions => ALL executable. Gw={Gw}", gateway.id);
                return (static _ => true, expectedExec: outgoingAll.Count);
            }

            var chosenMany = _selector.ChooseMany(outgoingAll, gateway, process, token);

            if (chosenMany is null || chosenMany.Count == 0)
            {
                var def = ResolveDefaultFlow(outgoingAll, defaultId);
                if (def is not null)
                {
                    var defKey = FlowKey(def);
                    return (f => StringComparer.OrdinalIgnoreCase.Equals(FlowKey(f), defKey), expectedExec: 1);
                }

                // Strict: if conditions exist but none match and no default => 0 executable (handled by caller => fail).
                _logger.LogWarning("[SPLIT] OR: no condition matched & no default. Gw={Gw}", gateway.id);
                return (static _ => false, expectedExec: 0);
            }

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < chosenMany.Count; i++)
                set.Add(FlowKey(chosenMany[i]));

            return (f => set.Contains(FlowKey(f)), expectedExec: set.Count);
        }

        // Unknown gateway: safest is all executable (but warn)
        _logger.LogWarning("[SPLIT] Unknown gateway type => ALL executable. Gw={Gw} Type={Type}", gateway.id, gateway.GetType().Name);
        return (static _ => true, expectedExec: outgoingAll.Count);
    }

    // -------------------- helpers (no LINQ hot-path) --------------------

    private static List<BpmnSequenceFlow> DeduplicateAndValidate(
        List<BpmnSequenceFlow> outgoingRaw,
        BpmnGateway gateway,
        Token token)
    {
        var dict = new Dictionary<string, BpmnSequenceFlow>(outgoingRaw.Count, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < outgoingRaw.Count; i++)
        {
            var f = outgoingRaw[i];
            var key = FlowKey(f);

            if (string.IsNullOrWhiteSpace(f.targetRef))
            {
                token.Fail($"Split gateway '{gateway.id}' has outgoing flow(s) with empty targetRef. Flow={key}");
                return new List<BpmnSequenceFlow>(0);
            }

            // Prefer id if exists; else source->target.
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
            if (string.Equals(f.id, defaultFlowId, StringComparison.OrdinalIgnoreCase)) return f;
            if (string.Equals(FlowKey(f), defaultFlowId, StringComparison.OrdinalIgnoreCase)) return f;
        }

        return null;
    }

    private static string FlowKey(BpmnSequenceFlow f)
        => !string.IsNullOrWhiteSpace(f.id) ? f.id! : $"{f.sourceRef}->{f.targetRef}";
}
