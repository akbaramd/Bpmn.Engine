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

        // 🔒 اگر توکن bypass/non-exec است: fork نکن (برای جلوگیری از انفجار)
        if (!token.IsExecutable)
        {
            var passFlow = ChooseDefaultOrFirst(outgoingAll, gateway);
            if (passFlow == null || string.IsNullOrWhiteSpace(passFlow.targetRef))
            {
                token.Fail($"NonExecutable token cannot pass split gateway '{gateway.id}': no valid outgoing.");
                return true;
            }

            _logger.LogWarning("[SPLIT] NonExecutable token => NO-FORK. Passing via {FlowKey} to {Target}",
                FlowKey(passFlow), passFlow.targetRef);

            // اگر توکن Active نیست، دوباره MoveTo باعث exception می‌شود
            if (token.State != TokenState.Active)
            {
                _logger.LogWarning("[SPLIT] NonExecutable token state is {State}, expected Active. Skipping MoveTo.", token.State);
                return true;
            }

            token.MoveTo(passFlow.targetRef!, FlowKey(passFlow));
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
        var expectedCount = selectedFlows
            .Select(FlowKey)
            .Distinct()
            .Count();

        // ثبت expected برای join های بعدی
        RegisterExpectedCount(process, scopeId, expectedCount);

        _logger.LogInformation("[SPLIT] ForkScopeId={ScopeId} ExpectedArrivals={Expected} Selected={Selected}",
            scopeId, expectedCount, string.Join(", ", selectedFlows.Select(f => $"{FlowKey(f)}=>{f.targetRef}")));

        // ✅ parent token is replaced by children (terminate/remove parent)
        token.Terminate($"Split gateway '{gateway.id}' forked to {expectedCount} branch(es).");
        process.RemoveToken(token.Id);

        // fork only selected flows, children inherit executable=true (parent is executable)
        await _fork.ForkChildrenAsync(
            process,
            parent: token,
            outgoing: selectedFlows,
            scopeId: scopeId,
            isExecutableForFlow: _ => true,
            ctx: ctx,
            ct: ct);

        _logger.LogInformation("[SPLIT] ForkChildrenAsync done. ParentTerminated={ParentState}", token.State);
        return true;
    }

    private static string ScopeExpectedKey(Guid scopeId) => $"{ScopeExpectedPrefix}{scopeId:N}";

    private void RegisterExpectedCount(Process process, Guid scopeId, int expectedCount)
    {
        // note: Process.SetVariable might not support null removal; we only set int
        process.SetVariable(ScopeExpectedKey(scopeId), expectedCount);
    }

    internal static bool TryReadExpectedCount(Process process, Guid scopeId, out int expected)
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
