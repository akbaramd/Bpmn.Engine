// Domain/Entities/Process.cs  (final: Variables + Metadata as single JSON blobs)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Domain.Entities;

public sealed class Process : BaseAggregateRoot
{
    public Guid ProjectId { get; private set; }
    public Guid DeploymentId { get; private set; }
    public string ProcessBpmnId { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? BusinessKey { get; private set; }

    public ProcessState State { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }
    public DateTime? TerminatedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
    public string? TerminationReason { get; private set; }

    private readonly HashSet<Guid> _tokenIds = new();
    public IReadOnlyCollection<Guid> TokenIds => _tokenIds;

    private readonly HashSet<Guid> _nodeInstanceIds = new();
    public IReadOnlyCollection<Guid> NodeInstanceIds => _nodeInstanceIds;

    // =========================
    // Variables (SINGLE JSON)
    // =========================
    private string _variablesJson = "{}";
    private JsonObject? _variablesObj;
    private bool _variablesLoaded;

    public string VariablesJson => _variablesJson;
    public JsonObject VariablesObject => GetVarsClone();

    // =========================
    // Metadata (SINGLE JSON)
    // =========================
    private string _metadataJson = "{}";
    private JsonObject? _metadataObj;
    private bool _metadataLoaded;

    public string MetadataJson => _metadataJson;
    public JsonObject MetadataObject => GetMetaClone();

    private Process()
    {
        State = ProcessState.Created;
        CreatedAtUtc = DateTime.UtcNow;

        _variablesJson = "{}";
        _variablesLoaded = false;
        _variablesObj = null;

        _metadataJson = "{}";
        _metadataLoaded = false;
        _metadataObj = null;
    }

    public static Process Create(
        Guid projectId,
        Guid deploymentId,
        string processBpmnId,
        string name,
        IDictionary<string, object?>? initialVariables = null,
        string? businessKey = null,
        IDictionary<string, JsonNode?>? initialMetadata = null)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId cannot be empty.", nameof(projectId));
        if (deploymentId == Guid.Empty) throw new ArgumentException("DeploymentId cannot be empty.", nameof(deploymentId));
        if (string.IsNullOrWhiteSpace(processBpmnId)) throw new ArgumentException("ProcessBpmnId cannot be empty.", nameof(processBpmnId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be empty.", nameof(name));

        var p = new Process
        {
            ProjectId = projectId,
            DeploymentId = deploymentId,
            ProcessBpmnId = processBpmnId.Trim(),
            Name = name.Trim(),
            BusinessKey = string.IsNullOrWhiteSpace(businessKey) ? null : businessKey.Trim(),
        };

        if (initialVariables is not null && initialVariables.Count > 0)
        {
            var patch = ProcessVariablesPatch.From(initialVariables, removals: null);
            p.ApplyVariablesPatch(patch);
        }

        if (initialMetadata is not null && initialMetadata.Count > 0)
        {
            var patch = ProcessMetadataPatch.From(initialMetadata, removals: null);
            p.ApplyMetadataPatch(patch);
        }

        p.AddDomainEvent(new ProcessInstanceCreatedEvent(
            p.Id,
            p.ProjectId,
            p.DeploymentId,
            p.ProcessBpmnId,
            p.BusinessKey,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["$"] = p._variablesJson,
                ["$meta"] = p._metadataJson
            },
            p.CreatedAtUtc));

        return p;
    }

    // ---- Lifecycle ----
    public void Start()
    {
        EnsureState(ProcessState.Created);
        State = ProcessState.Running;
        StartedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new ProcessStartedEvent(Id, StartedAtUtc.Value));
    }

    public void Complete()
    {
        EnsureState(ProcessState.Running);
        State = ProcessState.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new ProcessCompletedEvent(Id, CompletedAtUtc.Value));
    }

    public void Fail(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("reason required", nameof(reason));
        if (State is ProcessState.Completed or ProcessState.Terminated or ProcessState.Failed)
            throw new InvalidOperationException($"Cannot fail in state {State}");

        State = ProcessState.Failed;
        FailedAtUtc = DateTime.UtcNow;
        FailureReason = reason;
        AddDomainEvent(new ProcessFailedEvent(Id, FailedAtUtc.Value, reason));
    }

    public void Terminate(string? reason = null)
    {
        if (State is ProcessState.Completed or ProcessState.Terminated)
            throw new InvalidOperationException($"Cannot terminate in state {State}");

        State = ProcessState.Terminated;
        TerminatedAtUtc = DateTime.UtcNow;
        TerminationReason = reason;
        AddDomainEvent(new ProcessTerminatedEvent(Id, TerminatedAtUtc.Value, reason));
    }

    // ---- Ownership ----
    public void AddToken(Guid tokenId)
    {
        EnsureCanAcceptRuntimeMutations();
        if (tokenId == Guid.Empty) throw new ArgumentException("tokenId empty", nameof(tokenId));
        _tokenIds.Add(tokenId);
    }

    public void RemoveToken(Guid tokenId) => _tokenIds.Remove(tokenId);

    public void RegisterNodeInstance(Guid nodeInstanceId)
    {
        EnsureCanAcceptRuntimeMutations();
        if (nodeInstanceId == Guid.Empty) throw new ArgumentException("nodeInstanceId empty", nameof(nodeInstanceId));
        _nodeInstanceIds.Add(nodeInstanceId);
    }

    public void UnregisterNodeInstance(Guid nodeInstanceId) => _nodeInstanceIds.Remove(nodeInstanceId);

    // =========================
    // Variables API
    // =========================
    public bool HasVariable(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        var k = NormalizeKey(key);
        return Vars().ContainsKey(k);
    }

    public JsonNode? GetVariableNode(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var k = NormalizeKey(key);
        var vars = Vars();
        return vars.TryGetPropertyValue(k, out var node) ? node : null;
    }

    public bool TryGetVariable<T>(string key, out T? value)
    {
        value = default;
        var node = GetVariableNode(key);
        if (node is null) return false;

        try
        {
            value = node.Deserialize<T>(JsonVariableCodec.Options);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public T? GetVariable<T>(string key, T? defaultValue = default)
        => TryGetVariable<T>(key, out var v) ? v : defaultValue;

    public void SetVariable(string key, object? value)
    {
        EnsureCanAcceptRuntimeMutations();

        var k = EnsureKey(key);
        var vars = Vars();

        var node = JsonVariableCodec.ToNode(value);
        var newJson = JsonVariableCodec.ToStableJson(node);

        if (vars.TryGetPropertyValue(k, out var oldNode))
        {
            var oldJson = JsonVariableCodec.ToStableJson(oldNode);
            if (string.Equals(oldJson, newJson, StringComparison.Ordinal))
                return;
        }

        vars[k] = node;
        FlushVars(vars);

        AddDomainEvent(new ProcessVariablesChangedEvent(
            Id,
            new Dictionary<string, string>(StringComparer.Ordinal) { [k] = newJson },
            Array.Empty<string>(),
            DateTime.UtcNow));
    }

    public void RemoveVariable(string key)
    {
        EnsureCanAcceptRuntimeMutations();

        if (string.IsNullOrWhiteSpace(key)) return;
        var k = NormalizeKey(key);

        var vars = Vars();
        if (!vars.Remove(k))
            return;

        FlushVars(vars);

        AddDomainEvent(new ProcessVariablesChangedEvent(
            Id,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new List<string> { k }.AsReadOnly(),
            DateTime.UtcNow));
    }

    public void ApplyVariablesPatch(ProcessVariablesPatch patch)
    {
        if (patch is null) throw new ArgumentNullException(nameof(patch));
        if (!patch.HasChanges) return;

        EnsureCanAcceptRuntimeMutations();

        var vars = Vars();
        var upsertsActual = new Dictionary<string, string>(StringComparer.Ordinal);
        var removalsActual = new List<string>();

        foreach (var raw in patch.Removals ?? Array.Empty<string>())
        {
            var k = NormalizeKey(raw);
            if (k.Length == 0) continue;

            if (vars.Remove(k))
                removalsActual.Add(k);
        }

        foreach (var kv in patch.Upserts ?? new Dictionary<string, JsonNode?>(StringComparer.Ordinal))
        {
            var k = NormalizeKey(kv.Key);
            if (k.Length == 0) continue;

            var node = kv.Value;
            var newJson = JsonVariableCodec.ToStableJson(node);

            if (vars.TryGetPropertyValue(k, out var oldNode))
            {
                var oldJson = JsonVariableCodec.ToStableJson(oldNode);
                if (string.Equals(oldJson, newJson, StringComparison.Ordinal))
                    continue;
            }

            vars[k] = node;
            upsertsActual[k] = newJson;
        }

        if (upsertsActual.Count == 0 && removalsActual.Count == 0)
            return;

        FlushVars(vars);

        AddDomainEvent(new ProcessVariablesChangedEvent(
            Id,
            upsertsActual,
            removalsActual.AsReadOnly(),
            DateTime.UtcNow));
    }

    // =========================
    // Metadata API
    // =========================
    public bool HasMetadata(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        var k = NormalizeKey(key);
        return Meta().ContainsKey(k);
    }

    public JsonNode? GetMetadataNode(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var k = NormalizeKey(key);
        var meta = Meta();
        return meta.TryGetPropertyValue(k, out var node) ? node : null;
    }

    public bool TryGetMetadata<T>(string key, out T? value)
    {
        value = default;
        var node = GetMetadataNode(key);
        if (node is null) return false;

        try
        {
            value = node.Deserialize<T>(JsonVariableCodec.Options);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public T? GetMetadata<T>(string key, T? defaultValue = default)
        => TryGetMetadata<T>(key, out var v) ? v : defaultValue;

    public void SetMetadata(string key, object? value)
    {
        EnsureCanAcceptRuntimeMutations();

        var k = EnsureKey(key);
        var meta = Meta();

        var node = JsonVariableCodec.ToNode(value);
        var newJson = JsonVariableCodec.ToStableJson(node);

        if (meta.TryGetPropertyValue(k, out var oldNode))
        {
            var oldJson = JsonVariableCodec.ToStableJson(oldNode);
            if (string.Equals(oldJson, newJson, StringComparison.Ordinal))
                return;
        }

        meta[k] = node;
        FlushMeta(meta);

        AddDomainEvent(new ProcessMetadataChangedEvent(
            Id,
            new Dictionary<string, string>(StringComparer.Ordinal) { [k] = newJson },
            Array.Empty<string>(),
            DateTime.UtcNow));
    }

    public void RemoveMetadata(string key)
    {
        EnsureCanAcceptRuntimeMutations();

        if (string.IsNullOrWhiteSpace(key)) return;
        var k = NormalizeKey(key);

        var meta = Meta();
        if (!meta.Remove(k))
            return;

        FlushMeta(meta);

        AddDomainEvent(new ProcessMetadataChangedEvent(
            Id,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new List<string> { k }.AsReadOnly(),
            DateTime.UtcNow));
    }

    public void ApplyMetadataPatch(ProcessMetadataPatch patch)
    {
        if (patch is null) throw new ArgumentNullException(nameof(patch));
        if (!patch.HasChanges) return;

        EnsureCanAcceptRuntimeMutations();

        var meta = Meta();
        var upsertsActual = new Dictionary<string, string>(StringComparer.Ordinal);
        var removalsActual = new List<string>();

        foreach (var raw in patch.Removals ?? Array.Empty<string>())
        {
            var k = NormalizeKey(raw);
            if (k.Length == 0) continue;

            if (meta.Remove(k))
                removalsActual.Add(k);
        }

        foreach (var kv in patch.Upserts ?? new Dictionary<string, JsonNode?>(StringComparer.Ordinal))
        {
            var k = NormalizeKey(kv.Key);
            if (k.Length == 0) continue;

            var node = kv.Value;
            var newJson = JsonVariableCodec.ToStableJson(node);

            if (meta.TryGetPropertyValue(k, out var oldNode))
            {
                var oldJson = JsonVariableCodec.ToStableJson(oldNode);
                if (string.Equals(oldJson, newJson, StringComparison.Ordinal))
                    continue;
            }

            meta[k] = node;
            upsertsActual[k] = newJson;
        }

        if (upsertsActual.Count == 0 && removalsActual.Count == 0)
            return;

        FlushMeta(meta);

        AddDomainEvent(new ProcessMetadataChangedEvent(
            Id,
            upsertsActual,
            removalsActual.AsReadOnly(),
            DateTime.UtcNow));
    }

    // ---- internals (Vars) ----
    private JsonObject Vars()
    {
        if (_variablesLoaded && _variablesObj is not null)
            return _variablesObj;

        _variablesObj = JsonVariableCodec.ParseObjectOrEmpty(_variablesJson);
        _variablesLoaded = true;
        return _variablesObj;
    }

    private void FlushVars(JsonObject vars)
    {
        _variablesObj = vars;
        _variablesLoaded = true;
        _variablesJson = vars.ToJsonString(JsonVariableCodec.Options);
    }

    private JsonObject GetVarsClone()
    {
        var vars = Vars();
        var json = vars.ToJsonString(JsonVariableCodec.Options);
        return JsonVariableCodec.ParseObjectOrEmpty(json);
    }

    // ---- internals (Meta) ----
    private JsonObject Meta()
    {
        if (_metadataLoaded && _metadataObj is not null)
            return _metadataObj;

        _metadataObj = JsonVariableCodec.ParseObjectOrEmpty(_metadataJson);
        _metadataLoaded = true;
        return _metadataObj;
    }

    private void FlushMeta(JsonObject meta)
    {
        _metadataObj = meta;
        _metadataLoaded = true;
        _metadataJson = meta.ToJsonString(JsonVariableCodec.Options);
    }

    private JsonObject GetMetaClone()
    {
        var meta = Meta();
        var json = meta.ToJsonString(JsonVariableCodec.Options);
        return JsonVariableCodec.ParseObjectOrEmpty(json);
    }

    private static string NormalizeKey(string key) => (key ?? string.Empty).Trim();

    private static string EnsureKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty.", nameof(key));
        return key.Trim();
    }

    private void EnsureState(ProcessState required)
    {
        if (State != required)
            throw new InvalidOperationException($"Process must be {required} but is {State}");
    }

    private void EnsureCanAcceptRuntimeMutations()
    {
        if (State is ProcessState.Completed or ProcessState.Terminated or ProcessState.Failed)
            throw new InvalidOperationException($"Process in {State} is immutable.");
    }
}
