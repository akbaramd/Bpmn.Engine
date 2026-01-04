using System.Text.Json;
using System.Text.Json.Nodes;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

public enum NodeIoMappingPhase
{
    InputApplied = 1,
    OutputApplied = 2
}

public enum NodeIoMappingKind
{
    Variable = 1,
    Feel = 2
}

public sealed record NodeIoMappingSnapshot(
    NodeIoMappingPhase Phase,
    string ElementId,
    Guid ProcessId,
    Guid TokenId,
    DateTime OccurredAtUtc,
    IReadOnlyList<NodeIoMappingEntry> Entries,
    IReadOnlyDictionary<string, string>? TokenVarsAfterJson,
    IReadOnlyDictionary<string, string>? ProcessVarsAfterJson);

public sealed record NodeIoMappingEntry(
    string? Source,
    string Target,
    NodeIoMappingKind Kind,
    bool Success,
    string? ValueJson,
    MissingBehavior? MissingBehavior,
    bool AppliedAsNull,
    bool Failed,
    string? Reason)
{
    public static NodeIoMappingEntry Mapped(string source, string target, string valueJson, NodeIoMappingKind kind)
        => new(source, target, kind, true, valueJson, null, false, false, null);

    public static NodeIoMappingEntry Missing(string? source, string target, string reason, MissingBehavior behavior, bool appliedAsNull, bool failed)
        => new(source, target, NodeIoMappingKind.Variable, false, null, behavior, appliedAsNull, failed, reason);

    public static NodeIoMappingEntry Skipped(string? source, string? target, string reason)
        => new(source, (target ?? string.Empty).Trim(), NodeIoMappingKind.Variable, false, null, null, false, false, reason);
}

public sealed class BonyanVariableMappingService : IVariableMappingService
{
    private static readonly JsonNode JsonNull = JsonNode.Parse("null")!;

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

    public void ApplyInputs(Process process, Token token, NodeInstance node, BpmnFlowElement element, BpmnRuntimeContext ctx)
    {
        if (token.State is TokenState.Completed or TokenState.Terminated) return;

        _logger.LogDebug(
            "[MAP:IN] Starting input mapping. ElementId={ElementId} ProcessId={ProcessId} TokenId={TokenId}",
            element.id, process.Id, token.Id);

        var map = GetIoMapping(element);
        if (map is null || map.Input.Count == 0)
        {
            _logger.LogDebug("[MAP:IN] No input mapping found. Element={ElementId}", element.id);
            return;
        }

        var entries = new List<NodeIoMappingEntry>(map.Input.Count);
        var mappedCount = 0;

        foreach (var input in map.Input)
        {
            var srcRaw = input.Source?.Trim();
            var tgt = input.Target?.Trim();

            if (string.IsNullOrWhiteSpace(tgt))
            {
                entries.Add(NodeIoMappingEntry.Skipped(srcRaw, tgt, "empty target"));
                continue;
            }

            if (string.IsNullOrWhiteSpace(srcRaw))
            {
                ApplyMissingInput(map.OnMissingSource, token, tgt, "empty source", entries, srcRaw);
                continue;
            }

            var isFeel = srcRaw.StartsWith("=", StringComparison.Ordinal);
            object? valueObj;

            if (!isFeel)
            {
                var nodeVal = process.GetVariableNode(srcRaw);
                if (nodeVal is null)
                {
                    ApplyMissingInput(map.OnMissingSource, token, tgt, $"missing process var '{srcRaw}'", entries, srcRaw);
                    continue;
                }

                valueObj = JsonVariableCodec.CloneNode(nodeVal);
            }
            else
            {
                var expr = srcRaw[1..].Trim();
                try
                {
                    var feelCtx = BuildFeelContext(process);
                    valueObj = _feel.Evaluate(expr, feelCtx);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[MAP:IN] FEEL eval failed. Expr={Expr} Element={ElementId}", expr, element.id);
                    ApplyMissingInput(map.OnMissingSource, token, tgt, "feel eval failed", entries, srcRaw);
                    continue;
                }
            }

            token.SetVariable(tgt, valueObj);
            mappedCount++;

            entries.Add(NodeIoMappingEntry.Mapped(
                source: srcRaw,
                target: tgt,
                valueJson: ToStableJson(valueObj),
                kind: isFeel ? NodeIoMappingKind.Feel : NodeIoMappingKind.Variable));
        }

        var tokenSnapshot = StableJsonVarsFromToken(token.Variables);


        _logger.LogInformation(
            "[MAP:IN] ✅ Input mapping completed. ElementId={ElementId} MappedCount={MappedCount} TokenVarsCount={TokenVarsCount}",
            element.id, mappedCount, tokenSnapshot.Count);
    }

    public void ApplyOutputs(Process process, Token token, NodeInstance node, BpmnFlowElement element, BpmnRuntimeContext ctx)
    {
        if (token.State is TokenState.Completed or TokenState.Terminated) return;

        _logger.LogDebug(
            "[MAP:OUT] Starting output mapping. ElementId={ElementId} ProcessId={ProcessId} TokenId={TokenId}",
            element.id, process.Id, token.Id);

        var map = GetIoMapping(element);
        if (map is null || map.Output.Count == 0)
        {
            _logger.LogDebug("[MAP:OUT] No output mapping found. Element={ElementId}", element.id);
            return;
        }

        var entries = new List<NodeIoMappingEntry>(map.Output.Count);
        var mappedCount = 0;

        foreach (var output in map.Output)
        {
            var src = output.Source?.Trim();
            var tgt = output.Target?.Trim();

            if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(tgt))
            {
                entries.Add(NodeIoMappingEntry.Skipped(src, tgt, "empty source or target"));
                continue;
            }

            if (!token.Variables.TryGetValue(src, out var tokenNode))
            {
                ApplyMissingOutput(map.OnMissingOutput, process, token, tgt, $"missing token var '{src}'", entries, src);
                continue;
            }

            if (!map.Overwrite && process.HasVariable(tgt))
            {
                entries.Add(NodeIoMappingEntry.Skipped(src, tgt, "overwrite=false and process already has target"));
                continue;
            }

            var cloned = JsonVariableCodec.CloneNode(tokenNode) ?? JsonNull;
            process.SetVariable(tgt, cloned);
            mappedCount++;

            entries.Add(NodeIoMappingEntry.Mapped(
                source: src,
                target: tgt,
                valueJson: JsonVariableCodec.ToStableJson(cloned),
                kind: NodeIoMappingKind.Variable));
        }

        var processSnapshot = StableJsonVarsFromProcess(process);

       

        _logger.LogInformation(
            "[MAP:OUT] ✅ Output mapping completed. ElementId={ElementId} MappedCount={MappedCount} ProcessVarsCount={ProcessVarsCount}",
            element.id, mappedCount, processSnapshot.Count);
    }

    private BonyanIoMapping? GetIoMapping(BpmnFlowElement element)
        => _ioAccessor.TryGetIoMapping(element, out var mapping) ? mapping : null;

    private void ApplyMissingInput(
        MissingBehavior policy,
        Token token,
        string target,
        string reason,
        List<NodeIoMappingEntry> entries,
        string? source)
    {
        switch (policy)
        {
            case MissingBehavior.Skip:
                entries.Add(NodeIoMappingEntry.Missing(source, target, reason, policy, appliedAsNull: false, failed: false));
                break;

            case MissingBehavior.Null:
                token.SetVariable(target, JsonNull);
                entries.Add(NodeIoMappingEntry.Missing(source, target, reason, policy, appliedAsNull: true, failed: false));
                break;

            case MissingBehavior.Throw:
                token.Fail($"IO input missing for '{target}': {reason}");
                entries.Add(NodeIoMappingEntry.Missing(source, target, reason, policy, appliedAsNull: false, failed: true));
                break;
        }
    }

    private void ApplyMissingOutput(
        MissingBehavior policy,
        Process process,
        Token token,
        string target,
        string reason,
        List<NodeIoMappingEntry> entries,
        string? source)
    {
        switch (policy)
        {
            case MissingBehavior.Skip:
                entries.Add(NodeIoMappingEntry.Missing(source, target, reason, policy, appliedAsNull: false, failed: false));
                break;

            case MissingBehavior.Null:
                process.SetVariable(target, JsonNull);
                entries.Add(NodeIoMappingEntry.Missing(source, target, reason, policy, appliedAsNull: true, failed: false));
                break;

            case MissingBehavior.Throw:
                token.Fail($"IO output missing for '{target}': {reason}");
                entries.Add(NodeIoMappingEntry.Missing(source, target, reason, policy, appliedAsNull: false, failed: true));
                break;
        }
    }

    private static IReadOnlyDictionary<string, object?> BuildFeelContext(Process process)
    {
        var obj = process.VariablesObject;
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var kv in obj)
        {
            var key = (kv.Key ?? string.Empty).Trim();
            if (key.Length == 0) continue;

            dict[key] = JsonNodeToDotNet(kv.Value);
        }

        return dict;
    }

    private static object? JsonNodeToDotNet(JsonNode? node)
    {
        if (node is null) return null;

        try
        {
            return node.Deserialize<object>(JsonVariableCodec.Options);
        }
        catch
        {
            return JsonVariableCodec.ToStableJson(node);
        }
    }

    private static string ToStableJson(object? value)
    {
        if (value is null) return "null";
        if (value is JsonNode jn) return JsonVariableCodec.ToStableJson(jn);
        return JsonVariableCodec.ToStableJson(JsonVariableCodec.ToNode(value));
    }

    private static IReadOnlyDictionary<string, string> StableJsonVarsFromToken(IReadOnlyDictionary<string, JsonNode?> vars)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in vars)
        {
            var k = (kv.Key ?? string.Empty).Trim();
            if (k.Length == 0) continue;

            dict[k] = JsonVariableCodec.ToStableJson(kv.Value);
        }
        return dict;
    }

    private static IReadOnlyDictionary<string, string> StableJsonVarsFromProcess(Process process)
    {
        var obj = process.VariablesObject;
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var kv in obj)
        {
            var k = (kv.Key ?? string.Empty).Trim();
            if (k.Length == 0) continue;

            dict[k] = JsonVariableCodec.ToStableJson(kv.Value);
        }

        return dict;
    }


}