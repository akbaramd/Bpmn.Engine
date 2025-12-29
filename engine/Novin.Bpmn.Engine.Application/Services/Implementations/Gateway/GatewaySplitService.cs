using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;
using System;
using System.Collections.Generic;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class GatewaySplitService : IGatewaySplitService
{
    private readonly ITokenForkService _fork;
    private readonly ISequenceFlowSelector _selector;
    private readonly ILogger<GatewaySplitService> _logger;

    private const string ExpectedTotalPrefix = "__novin.scope.expectedTotal:";
    private const string ExpectedExecPrefix  = "__novin.scope.expectedExec:";

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

        var outgoingRaw = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);
        if (outgoingRaw == null || outgoingRaw.Count <= 1)
            return false;

        if (token.State != TokenState.Active)
        {
            _logger.LogWarning("[SPLIT] Ignored. Token state={State} (expected Active). TokenId={TokenId} Gw={Gw}",
                token.State, token.Id, gateway.id);
            return true;
        }

        var outgoingAll = DeduplicateOutgoing(outgoingRaw, gateway, token);
        if (outgoingAll.Count <= 1)
            return false;

        // ---- Decide which outgoing become executable ----
        // IMPORTANT: if parent is trace => ALL children trace, NO condition evaluation.
        var (execPredicate, expectedExec) = BuildExecutability(process, token, gateway, outgoingAll);

        var scopeId = Guid.NewGuid();

        // ---- Store counts for join/merge semantics ----
        RegisterExpectedTotal(process, scopeId, outgoingAll.Count);
        RegisterExpectedExec(process, scopeId, expectedExec);

        _logger.LogInformation(
            "[SPLIT] Gw={Gw} Type={Type} TokenId={TokenId} ParentExec={ParentExec} ScopeId={ScopeId} Total={Total} ExecExpected={ExecExpected}",
            gateway.id, gateway.GetType().Name, token.Id, token.IsExecutable, scopeId, outgoingAll.Count, expectedExec);

        // consume parent
        token.Terminate($"Split gateway '{gateway.id}' created {outgoingAll.Count} branch token(s).");
        process.RemoveToken(token.Id);

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

    // -------------------- Expected counts --------------------

    private static string TotalKey(Guid scopeId) => $"{ExpectedTotalPrefix}{scopeId:N}";
    private static string ExecKey(Guid scopeId)  => $"{ExpectedExecPrefix}{scopeId:N}";

    private static void RegisterExpectedTotal(Process p, Guid scopeId, int total)
        => p.SetVariable(TotalKey(scopeId), total);

    private static void RegisterExpectedExec(Process p, Guid scopeId, int exec)
        => p.SetVariable(ExecKey(scopeId), exec);

    public static bool TryReadExpectedTotal(Process p, Guid scopeId, out int total)
    {
        total = 0;
        var k = TotalKey(scopeId);
        if (!p.HasVariable(k)) return false;
        return int.TryParse(p.GetVariable(k), out total);
    }

    public static bool TryReadExpectedExec(Process p, Guid scopeId, out int exec)
    {
        exec = 0;
        var k = ExecKey(scopeId);
        if (!p.HasVariable(k)) return false;
        return int.TryParse(p.GetVariable(k), out exec);
    }

    // -------------------- Executability rules --------------------

    private (Func<BpmnSequenceFlow, bool> predicate, int expectedExec) BuildExecutability(
        Process process,
        Token token,
        BpmnGateway gateway,
        List<BpmnSequenceFlow> outgoingAll)
    {
        // Rule for TRACE input:
        // If the incoming token is non-executable => DO NOT evaluate conditions
        // and send TRACE to ALL outgoing.
        if (!token.IsExecutable)
            return (static _ => false, expectedExec: 0);

        // Parallel split => all executable
        if (gateway is BpmnParallelGateway)
            return (static _ => true, expectedExec: outgoingAll.Count);

        // Event-based: should be subscriptions; fallback here = all executable (safe)
        if (gateway is BpmnEventBasedGateway)
        {
            _logger.LogWarning("[SPLIT] EventBasedGateway in split-service. Fallback ALL executable. Gw={Gw}", gateway.id);
            return (static _ => true, expectedExec: outgoingAll.Count);
        }

        var hasAnyCondition = HasAnyCondition(outgoingAll);
        var defaultId = GetGatewayDefaultFlowId(gateway);
        var hasDefault = !string.IsNullOrWhiteSpace(defaultId);

        // Exclusive:
        // - if NO conditions AND NO default => ALL executable (your rule)
        // - else only chosen is exec, others trace
        if (gateway is BpmnExclusiveGateway)
        {
            if (!hasAnyCondition && !hasDefault)
            {
                _logger.LogWarning("[SPLIT] XOR: no conditions and no default => ALL executable. Gw={Gw}", gateway.id);
                return (static _ => true, expectedExec: outgoingAll.Count);
            }

            var chosen = _selector.ChooseOne(outgoingAll, gateway, process, token)
                         ?? ResolveDefaultFlow(outgoingAll, defaultId)
                         ?? outgoingAll[0];

            var chosenKey = FlowKey(chosen);
            return (f => StringComparer.OrdinalIgnoreCase.Equals(FlowKey(f), chosenKey), expectedExec: 1);
        }

        // Inclusive:
        // - if NO conditions => ALL executable
        // - else chosenMany exec, others trace
        if (gateway is BpmnInclusiveGateway)
        {
            if (!hasAnyCondition)
            {
                _logger.LogWarning("[SPLIT] OR: no conditions => ALL executable. Gw={Gw}", gateway.id);
                return (static _ => true, expectedExec: outgoingAll.Count);
            }

            var chosenMany = _selector.ChooseMany(outgoingAll, gateway, process, token);
            if (chosenMany == null || chosenMany.Count == 0)
            {
                var def = ResolveDefaultFlow(outgoingAll, defaultId);
                if (def != null)
                {
                    var defKey = FlowKey(def);
                    return (f => StringComparer.OrdinalIgnoreCase.Equals(FlowKey(f), defKey), expectedExec: 1);
                }

                _logger.LogWarning("[SPLIT] OR: selection empty & no default => ALL executable. Gw={Gw}", gateway.id);
                return (static _ => true, expectedExec: outgoingAll.Count);
            }

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < chosenMany.Count; i++)
                set.Add(FlowKey(chosenMany[i]));

            return (f => set.Contains(FlowKey(f)), expectedExec: set.Count);
        }

        // Unknown gateway => safest: all executable
        _logger.LogWarning("[SPLIT] Unknown gateway type => ALL executable. Gw={Gw} Type={Type}", gateway.id, gateway.GetType().Name);
        return (static _ => true, expectedExec: outgoingAll.Count);
    }

    // -------------------- helpers (no LINQ hot-path) --------------------

    private static List<BpmnSequenceFlow> DeduplicateOutgoing(
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

            if (!dict.ContainsKey(key))
                dict[key] = f;
        }

        var list = new List<BpmnSequenceFlow>(dict.Count);
        foreach (var kv in dict) list.Add(kv.Value);
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
