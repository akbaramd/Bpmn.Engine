using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class BonyanVariableMappingService : IVariableMappingService
{
    private readonly IFeelExpressionEvaluator _feel;
    private readonly IBonyanIoAccessor _ioAccessor;
    private readonly ILogger<BonyanVariableMappingService> _logger;

    public BonyanVariableMappingService(
        IFeelExpressionEvaluator feel,
        IBonyanIoAccessor ioAccessor,
        ILogger<BonyanVariableMappingService> logger)
    {
        _feel = feel ?? throw new ArgumentNullException(nameof(feel));
        _ioAccessor = ioAccessor ?? throw new ArgumentNullException(nameof(ioAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void ApplyInputs(Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx)
    {
        if (token.State is TokenState.Completed or TokenState.Terminated) return;

        _logger.LogDebug(
            "[MAP:IN] Starting input mapping. ElementId={ElementId} ProcessId={ProcessId} TokenId={TokenId}",
            element.id,
            process.Id,
            token.Id);

        // Log process variables before mapping
        var processVarsBefore = process.Variables.ToDictionary(kv => kv.Key, kv => kv.Value);
        _logger.LogDebug(
            "[MAP:IN] Process variables BEFORE mapping. ElementId={ElementId} Count={Count} Variables={Variables}",
            element.id,
            processVarsBefore.Count,
            string.Join(", ", processVarsBefore.Select(kv => $"{kv.Key}={kv.Value}")));

        // Log token variables before mapping
        var tokenVarsBefore = token.Variables.ToDictionary(kv => kv.Key, kv => kv.Value);
        _logger.LogDebug(
            "[MAP:IN] Token variables BEFORE mapping. ElementId={ElementId} Count={Count} Variables={Variables}",
            element.id,
            tokenVarsBefore.Count,
            string.Join(", ", tokenVarsBefore.Select(kv => $"{kv.Key}={kv.Value}")));

        var map = GetIoMapping(element);
        if (map == null || map.Input.Count == 0)
        {
            _logger.LogDebug("[MAP:IN] No input mapping found. Element={ElementId}", element.id);
            return;
        }

        _logger.LogDebug(
            "[MAP:IN] Found input mapping. ElementId={ElementId} InputCount={InputCount} OnMissingSource={OnMissingSource}",
            element.id,
            map.Input.Count,
            map.OnMissingSource);

        var mappedCount = 0;
        foreach (var input in map.Input)
        {
            var src = input.Source?.Trim();
            var tgt = input.Target?.Trim();

            if (string.IsNullOrWhiteSpace(tgt))
            {
                _logger.LogDebug("[MAP:IN] Skipping mapping - empty target. ElementId={ElementId}", element.id);
                continue;
            }

            if (string.IsNullOrWhiteSpace(src))
            {
                _logger.LogDebug("[MAP:IN] Empty source for target {Target}. ElementId={ElementId}", tgt, element.id);
                HandleMissingInput(map.OnMissingSource, token, tgt, "empty source");
                continue;
            }

            object? value;
            var isFeel = src.StartsWith("=");

            if (!isFeel)
            {
                // plain process var
                if (!process.Variables.TryGetValue(src, out var stringValue))
                {
                    _logger.LogDebug(
                        "[MAP:IN] Process variable not found. ElementId={ElementId} Source={Source} Target={Target}",
                        element.id,
                        src,
                        tgt);
                    HandleMissingInput(map.OnMissingSource, token, tgt, $"missing process var '{src}'");
                    continue;
                }
                value = stringValue;
            }
            else
            {
                // FEEL expression based on process vars
                var expr = src.Substring(1).Trim();
                try
                {
                    var processVarsAsObjects = ConvertToStringObjectDictionary(process.Variables);
                    value = _feel.Evaluate(expr, processVarsAsObjects);
                    _logger.LogDebug(
                        "[MAP:IN] FEEL expression evaluated. ElementId={ElementId} Expr={Expr} Result={Result}",
                        element.id,
                        expr,
                        value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[MAP:IN] FEEL eval failed. Expr={Expr} Element={ElementId}", expr, element.id);
                    HandleMissingInput(map.OnMissingSource, token, tgt, "feel eval failed");
                    continue;
                }
            }

            token.SetVariable(tgt, value);
            mappedCount++;
            _logger.LogDebug("[MAP:IN] ✅ Mapped. ElementId={ElementId} {Src} -> {Tgt} = {Val}", element.id, src, tgt, value);
        }

        // Log token variables after mapping
        var tokenVarsAfter = token.Variables.ToDictionary(kv => kv.Key, kv => kv.Value);
        _logger.LogInformation(
            "[MAP:IN] ✅ Input mapping completed. ElementId={ElementId} MappedCount={MappedCount} TokenVarsCount={TokenVarsCount}",
            element.id,
            mappedCount,
            tokenVarsAfter.Count);
        _logger.LogDebug(
            "[MAP:IN] Token variables AFTER mapping. ElementId={ElementId} Variables={Variables}",
            element.id,
            string.Join(", ", tokenVarsAfter.Select(kv => $"{kv.Key}={kv.Value}")));
    }

    public void ApplyOutputs(Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx)
    {
        if (token.State is TokenState.Completed or TokenState.Terminated) return;

        _logger.LogDebug(
            "[MAP:OUT] Starting output mapping. ElementId={ElementId} ProcessId={ProcessId} TokenId={TokenId}",
            element.id,
            process.Id,
            token.Id);

        // Log token variables before mapping
        var tokenVarsBefore = token.Variables.ToDictionary(kv => kv.Key, kv => kv.Value);
        _logger.LogDebug(
            "[MAP:OUT] Token variables BEFORE mapping. ElementId={ElementId} Count={Count} Variables={Variables}",
            element.id,
            tokenVarsBefore.Count,
            string.Join(", ", tokenVarsBefore.Select(kv => $"{kv.Key}={kv.Value}")));

        // Log process variables before mapping
        var processVarsBefore = process.Variables.ToDictionary(kv => kv.Key, kv => kv.Value);
        _logger.LogDebug(
            "[MAP:OUT] Process variables BEFORE mapping. ElementId={ElementId} Count={Count} Variables={Variables}",
            element.id,
            processVarsBefore.Count,
            string.Join(", ", processVarsBefore.Select(kv => $"{kv.Key}={kv.Value}")));

        var map = GetIoMapping(element);
        if (map == null || map.Output.Count == 0)
        {
            _logger.LogDebug("[MAP:OUT] No output mapping found. Element={ElementId}", element.id);
            return;
        }

        _logger.LogDebug(
            "[MAP:OUT] Found output mapping. ElementId={ElementId} OutputCount={OutputCount} OnMissingOutput={OnMissingOutput} Overwrite={Overwrite}",
            element.id,
            map.Output.Count,
            map.OnMissingOutput,
            map.Overwrite);

        var mappedCount = 0;
        foreach (var output in map.Output)
        {
            var src = output.Source?.Trim();
            var tgt = output.Target?.Trim();

            if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(tgt))
            {
                _logger.LogDebug("[MAP:OUT] Skipping mapping - empty source or target. ElementId={ElementId}", element.id);
                continue;
            }

            if (!token.TryGetVariable(src, out var val))
            {
                _logger.LogDebug(
                    "[MAP:OUT] Token variable not found. ElementId={ElementId} Source={Source} Target={Target}",
                    element.id,
                    src,
                    tgt);
                HandleMissingOutput(map.OnMissingOutput, process, token, tgt, $"missing token var '{src}'");
                continue;
            }

            // overwrite policy
            if (!map.Overwrite && process.Variables.ContainsKey(tgt))
            {
                _logger.LogDebug("[MAP:OUT] Skipping overwrite. ElementId={ElementId} Target={Tgt}", element.id, tgt);
                continue;
            }

            // ✅ درست: تغییر از طریق متد دامنه
            process.SetVariable(tgt, val);
            mappedCount++;
            _logger.LogDebug("[MAP:OUT] ✅ Mapped. ElementId={ElementId} {Src} -> {Tgt} = {Val}", element.id, src, tgt, val);
        }

        // Log process variables after mapping
        var processVarsAfter = process.Variables.ToDictionary(kv => kv.Key, kv => kv.Value);
        _logger.LogInformation(
            "[MAP:OUT] ✅ Output mapping completed. ElementId={ElementId} MappedCount={MappedCount} ProcessVarsCount={ProcessVarsCount}",
            element.id,
            mappedCount,
            processVarsAfter.Count);
        _logger.LogDebug(
            "[MAP:OUT] Process variables AFTER mapping. ElementId={ElementId} Variables={Variables}",
            element.id,
            string.Join(", ", processVarsAfter.Select(kv => $"{kv.Key}={kv.Value}")));
    }

    private BonyanIoMapping? GetIoMapping(BpmnFlowElement element)
    {
        // Use IBonyanIoAccessor which now correctly checks extensionElements.BonyanIoMapping first
        // and falls back to parsing from Any array if needed
        if (_ioAccessor.TryGetIoMapping(element, out var mapping))
            return mapping;

        return null;
    }

    private void HandleMissingInput(MissingBehavior policy, Token token, string target, string reason)
    {
        switch (policy)
        {
            case MissingBehavior.Skip:
                _logger.LogDebug("[MAP:IN] missing -> skip. Target={Target} Reason={Reason}", target, reason);
                break;

            case MissingBehavior.Null:
                token.SetVariable(target, null);
                _logger.LogDebug("[MAP:IN] missing -> null. Target={Target} Reason={Reason}", target, reason);
                break;

            case MissingBehavior.Throw:
                token.Fail($"IO input missing for '{target}': {reason}");
                _logger.LogWarning("[MAP:IN] missing -> FAIL. Target={Target} Reason={Reason}", target, reason);
                break;
        }
    }

    private void HandleMissingOutput(MissingBehavior policy, Process process, Token token, string target, string reason)
    {
        switch (policy)
        {
            case MissingBehavior.Skip:
                _logger.LogDebug("[MAP:OUT] missing -> skip. Target={Target} Reason={Reason}", target, reason);
                break;

            case MissingBehavior.Null:
                process.SetVariable(target, null);
                _logger.LogDebug("[MAP:OUT] missing -> null. Target={Target} Reason={Reason}", target, reason);
                break;

            case MissingBehavior.Throw:
                token.Fail($"IO output missing for '{target}': {reason}");
                _logger.LogWarning("[MAP:OUT] missing -> FAIL. Target={Target} Reason={Reason}", target, reason);
                break;
        }
    }

    private static IReadOnlyDictionary<string, string?> ConvertToStringObjectDictionary(IReadOnlyDictionary<string, string> stringDict)
    {
        return stringDict.ToDictionary(kv => kv.Key, kv => (string?)kv.Value);
    }
}