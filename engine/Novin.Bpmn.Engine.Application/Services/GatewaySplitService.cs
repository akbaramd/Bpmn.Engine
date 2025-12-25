using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

public sealed class GatewaySplitService : IGatewaySplitService
{
    private readonly ITokenForkService _fork;
    private readonly ISequenceFlowSelector _selector;
    private readonly ILogger<GatewaySplitService> _logger;

    // reserved internal key prefix
    private const string ScopeExpectedPrefix = "__novin.scope.expectedCount:";

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

        var outgoingAll = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);
        if (outgoingAll.Count <= 1)
            return false; // not a split

        // 🔒 Trace token (non-executable): create trace tokens for ALL outgoing flows
        if (!token.IsExecutable)
        {
            _logger.LogInformation("[SPLIT] Trace token => creating trace tokens for ALL outgoing flows. OutgoingCount={Count}",
                outgoingAll.Count);

            // Scope for this fork group
            var traceScopeId = Guid.NewGuid();
            var traceExpectedCount = outgoingAll
                .Select(FlowKey)
                .Distinct()
                .Count();

            RegisterExpectedCount(process, traceScopeId, traceExpectedCount);

            // Terminate parent trace token
            token.Terminate($"Trace token split gateway '{gateway.id}' forked to {traceExpectedCount} trace branch(es).");
            process.RemoveToken(token.Id);

            // Create trace tokens for ALL outgoing flows
            await _fork.ForkChildrenAsync(
                process,
                parent: token,
                outgoing: outgoingAll,
                scopeId: traceScopeId,
                isExecutableForFlow: _ => false, // All are trace tokens
                ctx: ctx,
                ct: ct);

            _logger.LogInformation("[SPLIT] Trace token fork completed. Created {Count} trace tokens.", traceExpectedCount);
            return true;
        }

        // توکن باید Active باشد
        if (token.State != TokenState.Active)
        {
            _logger.LogWarning("[SPLIT] Split called with token state={State} (expected Active). Ignoring.", token.State);
            return true; // handled defensively
        }

        // انتخاب شاخه‌ها
        IReadOnlyList<BpmnSequenceFlow> selectedFlows;

        if (gateway is BpmnParallelGateway)
        {
            selectedFlows = outgoingAll;
            _logger.LogInformation("[SPLIT] ParallelGateway => selected all: {Count}", selectedFlows.Count);
        }
        else if (gateway is BpmnExclusiveGateway)
        {
            var chosen = _selector.ChooseOne(outgoingAll, gateway, process, token);
            if (chosen == null)
            {
                var def = ChooseDefaultOrFirst(outgoingAll, gateway);
                if (def == null)
                {
                    token.Fail($"Exclusive gateway '{gateway.id}' returned no choice and no default.");
                    return true;
                }

                chosen = def;
                _logger.LogWarning("[SPLIT] Exclusive ChooseOne returned null => fallback to default/first {Flow}", FlowKey(chosen));
            }

            selectedFlows = new[] { chosen };
            _logger.LogInformation("[SPLIT] ExclusiveGateway => selected {Flow} -> {Target}", FlowKey(chosen), chosen.targetRef);
        }
        else if (gateway is BpmnInclusiveGateway)
        {
            var picked = _selector.ChooseMany(outgoingAll, gateway, process, token)?.ToList() ?? new List<BpmnSequenceFlow>();

            var defaultFlowId = GetGatewayDefaultFlowId(gateway);
            var defaultFlow = ResolveDefaultFlow(outgoingAll, defaultFlowId);

            // ✅ قانون: اگر حداقل یک مسیر غیر-default انتخاب شد => default حذف
            if (defaultFlow != null && picked.Count > 0)
            {
                var nonDefault = picked.Where(f => FlowKey(f) != FlowKey(defaultFlow)).ToList();
                if (nonDefault.Count > 0)
                    picked = nonDefault;
            }

            // اگر هیچی انتخاب نشد => default (اگر هست)
            if (picked.Count == 0)
            {
                if (defaultFlow == null)
                {
                    token.Fail($"Inclusive gateway '{gateway.id}' selected nothing and has no default.");
                    return true;
                }

                picked.Add(defaultFlow);
                _logger.LogWarning("[SPLIT] Inclusive => selected empty => using DEFAULT {Flow}", FlowKey(defaultFlow));
            }

            selectedFlows = picked;
            _logger.LogInformation("[SPLIT] InclusiveGateway => selectedCount={Cnt} Selected={Selected}",
                selectedFlows.Count, string.Join(", ", selectedFlows.Select(FlowKey)));
        }
        else
        {
            // fallback: treat as parallel
            selectedFlows = outgoingAll;
            _logger.LogWarning("[SPLIT] Unknown gateway type => fallback select all.");
        }

        // Validate selected flows
        if (selectedFlows.Count == 0)
        {
            token.Fail($"Split gateway '{gateway.id}' produced zero selected flows.");
            return true;
        }

        if (selectedFlows.Any(f => string.IsNullOrWhiteSpace(f.targetRef)))
        {
            token.Fail($"Split gateway '{gateway.id}' has selected flow(s) with empty targetRef.");
            return true;
        }

        // scope for this fork group
        var scopeId = Guid.NewGuid();
        // ✅ Token-Centric Model: Expected count is ALL outgoing flows (not just selected)
        // Join waits for unique arrivals from ALL incoming flows
        var expectedCount = outgoingAll
            .Select(FlowKey)
            .Distinct()
            .Count();

        // ثبت expected برای join های بعدی
        RegisterExpectedCount(process, scopeId, expectedCount);

        var selectedFlowKeysStr = string.Join(", ", selectedFlows.Select(f => $"{FlowKey(f)}=>{f.targetRef}"));
        var allFlowKeys = string.Join(", ", outgoingAll.Select(f => $"{FlowKey(f)}=>{f.targetRef}"));
        _logger.LogInformation(
            "[SPLIT] ForkScopeId={ScopeId} ExpectedArrivals={Expected} Selected={Selected} AllFlows={All}",
            scopeId, expectedCount, selectedFlowKeysStr, allFlowKeys);

        // ✅ parent token is replaced by children (terminate/remove parent)
        token.Terminate($"Split gateway '{gateway.id}' forked to {expectedCount} branch(es).");
        process.RemoveToken(token.Id);

        // ✅ Token-Centric Model: Create tokens for ALL outgoing flows
        // Selected flows get executable tokens, non-selected get trace tokens
        var selectedFlowKeys = selectedFlows.Select(FlowKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        
        await _fork.ForkChildrenAsync(
            process,
            parent: token,
            outgoing: outgoingAll, // ALL outgoing flows, not just selected
            scopeId: scopeId,
            isExecutableForFlow: flow => selectedFlowKeys.Contains(FlowKey(flow)), // Only selected are executable
            ctx: ctx,
            ct: ct);

        var executableCount = selectedFlows.Count;
        var traceCount = outgoingAll.Count - executableCount;
        _logger.LogInformation(
            "[SPLIT] Fork completed. ExecutableTokens={Exec} TraceTokens={Trace} Total={Total}",
            executableCount, traceCount, outgoingAll.Count);
        return true;
    }

    private static string ScopeExpectedKey(Guid scopeId) => $"{ScopeExpectedPrefix}{scopeId:N}";

    private void RegisterExpectedCount(Process process, Guid scopeId, int expectedCount)
    {
        // note: Process.SetVariable might not support null removal; we only set int
        process.SetVariable(ScopeExpectedKey(scopeId), expectedCount);
    }

    public static bool TryReadExpectedCount(Process process, Guid scopeId, out int expected)
    {
        expected = 0;

        var key = ScopeExpectedKey(scopeId);
        if (!process.HasVariable(key))
            return false;

        var raw = process.GetVariable(key);

        // robust conversions (EF could deserialize as long/JsonElement/string)
        if (raw is int i) { expected = i; return true; }
        if (raw is long l) { expected = checked((int)l); return true; }
        if (raw is string s && int.TryParse(s, out var p)) { expected = p; return true; }

        // if your process variables sometimes come as JsonElement:
        if (raw is System.Text.Json.JsonElement je)
        {
            if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetInt32(out var jv))
            { expected = jv; return true; }

            if (je.ValueKind == System.Text.Json.JsonValueKind.String && int.TryParse(je.GetString(), out var js))
            { expected = js; return true; }
        }

        return false;
    }

    private static BpmnSequenceFlow? ChooseDefaultOrFirst(IReadOnlyList<BpmnSequenceFlow> outgoing, BpmnGateway gateway)
    {
        var defId = GetGatewayDefaultFlowId(gateway);
        var def = ResolveDefaultFlow(outgoing, defId);
        return def ?? outgoing.FirstOrDefault();
    }

    private static BpmnSequenceFlow? ResolveDefaultFlow(IReadOnlyList<BpmnSequenceFlow> outgoing, string? defaultFlowId)
    {
        if (string.IsNullOrWhiteSpace(defaultFlowId))
            return null;

        return outgoing.FirstOrDefault(f =>
            string.Equals(f.id, defaultFlowId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FlowKey(f), defaultFlowId, StringComparison.OrdinalIgnoreCase));
    }

    private static string FlowKey(BpmnSequenceFlow f)
        => !string.IsNullOrWhiteSpace(f.id) ? f.id! : $"{f.sourceRef}->{f.targetRef}";

    private static string? GetGatewayDefaultFlowId(BpmnGateway gateway)
    {
        var gt = gateway.GetType();
        return gt.GetProperty("default")?.GetValue(gateway) as string
               ?? gt.GetProperty("Default")?.GetValue(gateway) as string;
    }
}
