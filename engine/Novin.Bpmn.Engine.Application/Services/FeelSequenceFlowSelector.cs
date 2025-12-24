using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Services.Feel;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class FeelSequenceFlowSelector : ISequenceFlowSelector
{
    private readonly IFeelExpressionEvaluator _feel;
    private readonly ILogger<FeelSequenceFlowSelector> _logger;

    // اگر خواستی FEEL بتواند process را هم ببیند true کن.
    private readonly bool _includeProcessVars;

    // برای لاگ امن: چند کلید اول را نشان بده
    private const int MaxVarKeysToLog = 20;

    public FeelSequenceFlowSelector(
        IFeelExpressionEvaluator feel,
        ILogger<FeelSequenceFlowSelector> logger,
        bool includeProcessVars = false)
    {
        _feel = feel ?? throw new ArgumentNullException(nameof(feel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _includeProcessVars = includeProcessVars;
    }

    public BpmnSequenceFlow ChooseOne(
        IReadOnlyList<BpmnSequenceFlow> outgoing,
        BpmnGateway gateway,
        Process process,
        Token token)
    {
        if (outgoing is null) throw new ArgumentNullException(nameof(outgoing));
        if (gateway is null) throw new ArgumentNullException(nameof(gateway));
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (token is null) throw new ArgumentNullException(nameof(token));

        var vars = BuildEvalVars(process, token);

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProcessId"] = process.Id,
            ["TokenId"] = token.Id,
            ["GatewayId"] = gateway.id,
            ["GatewayType"] = gateway.GetType().Name,
            ["ElementId"] = token.CurrentElementId,
            ["ScopeId"] = token.ScopeId,
            ["ArrivedVia"] = token.ArrivedViaFlowId,
            ["Executable"] = token.IsExecutable,
            ["IncludeProcessVars"] = _includeProcessVars,
        }))
        {
            var defaultId = GetGatewayDefaultFlowId(gateway);

            _logger.LogInformation(
                "[SEL:ONE] Start. OutgoingCount={Count} DefaultId={DefaultId} VarsCount={VarsCount} VarsKeys={VarsKeys}",
                outgoing.Count,
                defaultId,
                vars.Count,
                string.Join(",", vars.Keys.Take(MaxVarKeysToLog)));

            // model order: first TRUE wins
            foreach (var f in outgoing)
            {
                var key = FlowKey(f);
                var cond = GetConditionText(f);
                var isUnconditional = string.IsNullOrWhiteSpace(cond);
                var isDefault = IsDefaultFlow(f, defaultId);

                _logger.LogDebug(
                    "[SEL:ONE] Flow={Flow} Target={Target} IsDefault={IsDefault} Unconditional={Uncond} Expr={Expr}",
                    key, f.targetRef, isDefault, isUnconditional, cond);

                if (isUnconditional)
                    continue; // XOR: no-condition را auto-true نمی‌گیریم

                var ok = SafeEval(cond!, vars, key, isInclusive: false);
                _logger.LogDebug("[SEL:ONE] EvalResult Flow={Flow} => {Result}", key, ok);

                if (ok)
                {
                    _logger.LogWarning("[SEL:ONE] CHOSEN Flow={Flow} Target={Target}", key, f.targetRef);
                    return f;
                }
            }

            // no match => default if exists
            if (!string.IsNullOrWhiteSpace(defaultId))
            {
                var df = outgoing.FirstOrDefault(x => FlowKey(x) == defaultId || x.id == defaultId);
                if (df != null)
                {
                    _logger.LogWarning("[SEL:ONE] No condition matched. Using DEFAULT Flow={Flow} Target={Target}",
                        FlowKey(df), df.targetRef);
                    return df;
                }
            }

            // fallback: unconditional as "otherwise"
            var otherwise = outgoing.FirstOrDefault(f => string.IsNullOrWhiteSpace(GetConditionText(f)));
            if (otherwise != null)
            {
                _logger.LogWarning("[SEL:ONE] No condition matched. Using UNCONDITIONAL Flow={Flow} Target={Target}",
                    FlowKey(otherwise), otherwise.targetRef);
                return otherwise;
            }

            _logger.LogError("[SEL:ONE] No condition matched and no default/unconditional. Falling back to FIRST outgoing.");
            return outgoing.First();
        }
    }

    public IReadOnlyList<BpmnSequenceFlow> ChooseMany(
        IReadOnlyList<BpmnSequenceFlow> outgoing,
        BpmnGateway gateway,
        Process process,
        Token token)
    {
        if (outgoing is null) throw new ArgumentNullException(nameof(outgoing));
        if (gateway is null) throw new ArgumentNullException(nameof(gateway));
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (token is null) throw new ArgumentNullException(nameof(token));

        var vars = BuildEvalVars(process, token);
        var selected = new List<BpmnSequenceFlow>();
        var defaultId = GetGatewayDefaultFlowId(gateway);

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProcessId"] = process.Id,
            ["TokenId"] = token.Id,
            ["GatewayId"] = gateway.id,
            ["GatewayType"] = gateway.GetType().Name,
            ["ElementId"] = token.CurrentElementId,
            ["ScopeId"] = token.ScopeId,
            ["ArrivedVia"] = token.ArrivedViaFlowId,
            ["Executable"] = token.IsExecutable,
            ["IncludeProcessVars"] = _includeProcessVars,
        }))
        {
            _logger.LogInformation(
                "[SEL:MANY] Start. OutgoingCount={Count} DefaultId={DefaultId} VarsCount={VarsCount} VarsKeys={VarsKeys}",
                outgoing.Count,
                defaultId,
                vars.Count,
                string.Join(",", vars.Keys.Take(MaxVarKeysToLog)));

            foreach (var f in outgoing)
            {
                var key = FlowKey(f);
                var cond = GetConditionText(f);
                var isUnconditional = string.IsNullOrWhiteSpace(cond);
                var isDefault = IsDefaultFlow(f, defaultId);

                _logger.LogDebug(
                    "[SEL:MANY] Flow={Flow} Target={Target} IsDefault={IsDefault} Unconditional={Uncond} Expr={Expr}",
                    key, f.targetRef, isDefault, isUnconditional, cond);

                if (isUnconditional)
                    continue; // no-condition برای fallback

                var ok = SafeEval(cond!, vars, key, isInclusive: true);
                _logger.LogDebug("[SEL:MANY] EvalResult Flow={Flow} => {Result}", key, ok);

                if (ok)
                    selected.Add(f);
            }

            if (selected.Count > 0)
            {
                _logger.LogWarning(
                    "[SEL:MANY] SELECTED Count={Count} Flows={Flows}",
                    selected.Count,
                    string.Join(", ", selected.Select(x => $"{FlowKey(x)}=>{x.targetRef}")));

                return selected;
            }

            // Inclusive semantics: if none selected => default
            _logger.LogWarning("[SEL:MANY] No conditions matched. Applying default/unconditional fallback.");

            if (!string.IsNullOrWhiteSpace(defaultId))
            {
                var df = outgoing.FirstOrDefault(x => FlowKey(x) == defaultId || x.id == defaultId);
                if (df != null)
                {
                    selected.Add(df);
                    _logger.LogWarning("[SEL:MANY] Fallback DEFAULT Flow={Flow} Target={Target}",
                        FlowKey(df), df.targetRef);
                    return selected;
                }

                _logger.LogError("[SEL:MANY] DefaultId present but not found among outgoing. DefaultId={DefaultId}", defaultId);
            }

            var unconditional = outgoing.FirstOrDefault(f => string.IsNullOrWhiteSpace(GetConditionText(f)));
            if (unconditional != null)
            {
                selected.Add(unconditional);
                _logger.LogWarning("[SEL:MANY] Fallback UNCONDITIONAL Flow={Flow} Target={Target}",
                    FlowKey(unconditional), unconditional.targetRef);
                return selected;
            }

            // worst-case: return none? در inclusive نباید
            _logger.LogError("[SEL:MANY] No default/unconditional fallback found. Returning FIRST outgoing as safety.");
            selected.Add(outgoing.First());
            return selected;
        }
    }

    private bool SafeEval(string expr, IReadOnlyDictionary<string, object?> vars, string flowKey, bool isInclusive)
    {
        try
        {
            var result = _feel.EvaluateBoolean(expr, vars);
            return result;
        }
        catch (Exception ex)
        {
            // اینجا دیگه silently false نمی‌کنیم؛ لاگ دقیق می‌گیریم تا بفهمیم چرا default می‌خوره
            _logger.LogError(
                ex,
                "[SEL:EVAL] EvalError Mode={Mode} Flow={Flow} Expr={Expr} VarsCount={VarsCount} VarsKeys={VarsKeys}",
                isInclusive ? "Inclusive" : "Exclusive",
                flowKey,
                expr,
                vars.Count,
                string.Join(",", vars.Keys.Take(MaxVarKeysToLog)));

            return false;
        }
    }

    private IReadOnlyDictionary<string, object?> BuildEvalVars(Process p, Token t)
    {
        // حالت پیشنهادی شما: فقط Token vars (چون IO Mapping قبلش انجام شده)
        if (!_includeProcessVars)
            return new Dictionary<string, object?>(t.Variables, StringComparer.Ordinal);

        // حالت سازگار با گذشته: token overrides process
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var kv in p.Variables) dict[kv.Key] = kv.Value;
        foreach (var kv in t.Variables) dict[kv.Key] = kv.Value;
        return dict;
    }

    // -------- condition extraction (robust) --------
    private static string? GetConditionText(BpmnSequenceFlow f)
    {
        var ce = f.conditionExpression;
        if (ce?.Text == null || ce.Text.Length == 0) return null;

        var raw = string.Join(string.Empty, ce.Text).Trim();
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    private static string? GetGatewayDefaultFlowId(BpmnGateway gateway)
    {
        var gt = gateway.GetType();
        return gt.GetProperty("default")?.GetValue(gateway) as string
            ?? gt.GetProperty("Default")?.GetValue(gateway) as string;
    }

    private static bool IsDefaultFlow(BpmnSequenceFlow f, string? defaultId)
    {
        if (string.IsNullOrWhiteSpace(defaultId)) return false;
        var key = FlowKey(f);
        return key == defaultId || f.id == defaultId;
    }

    private static string FlowKey(BpmnSequenceFlow f)
        => !string.IsNullOrWhiteSpace(f.id) ? f.id! : $"{f.sourceRef}->{f.targetRef}";
}
