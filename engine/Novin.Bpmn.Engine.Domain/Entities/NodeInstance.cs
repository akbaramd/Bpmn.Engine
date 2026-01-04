using System.Text.Json;
using System.Text.Json.Nodes;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Domain.Entities;

/// <summary>
/// Lifecycle state of a node execution instance.
/// </summary>
public enum NodeState
{
    Created,
    Processing,
    Waiting,
    Completed,
    Failed,
    Skipped
}

/// <summary>
/// A single execution instance of a BPMN node (element) for a specific Token within a Process.
/// Created ONLY when the token is executable (this rule should be enforced by application service).
/// </summary>

// Domain/Entities/NodeInstance.cs  (final variables implementation: single JSON blob, JsonNode per key, Zeebe-like)


public sealed class NodeInstance : BaseAggregateRoot
{
    public Guid ProcessId { get; private set; }
    public Guid TokenId { get; private set; }

    public string ElementId { get; private set; } = default!;
    public NodeState State { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public Guid? ScopeId { get; private set; }
    public Guid? ActivityInstanceId { get; private set; }

    private readonly List<string> _arrivedViaFlowIds = new();
    public IReadOnlyList<string> ArrivedViaFlowIds => _arrivedViaFlowIds.AsReadOnly();

    public Guid? WorkerId { get; private set; }
    public Guid? UserTaskId { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool IsExecutable { get; private set; }

    // =========================
    // Variables (SINGLE JSON)
    // =========================
    private string _variablesJson = "{}";
    private JsonObject? _variablesObj;
    private bool _variablesLoaded;

    public string VariablesJson => _variablesJson;

    public JsonObject VariablesObject => GetVarsClone();

    private NodeInstance() { } // EF

    public NodeInstance(
        Guid processId,
        Guid tokenId,
        string elementId,
        Guid? scopeId,
        Guid? activityInstanceId,
        IEnumerable<string>? arrivedViaFlowIds,
        bool isExecutable)
    {
        if (processId == Guid.Empty) throw new ArgumentException("ProcessId cannot be empty.", nameof(processId));
        if (tokenId == Guid.Empty) throw new ArgumentException("TokenId cannot be empty.", nameof(tokenId));
        if (string.IsNullOrWhiteSpace(elementId)) throw new ArgumentException("ElementId is required.", nameof(elementId));

        ProcessId = processId;
        TokenId = tokenId;
        ElementId = elementId.Trim();

        ScopeId = scopeId;
        ActivityInstanceId = activityInstanceId;

        if (arrivedViaFlowIds != null)
        {
            foreach (var flowId in arrivedViaFlowIds)
            {
                if (!string.IsNullOrWhiteSpace(flowId))
                    _arrivedViaFlowIds.Add(flowId.Trim());
            }
        }

        IsExecutable = isExecutable;

        State = NodeState.Created;
        CreatedAtUtc = DateTime.UtcNow;

        _variablesJson = "{}";
        _variablesLoaded = false;
        _variablesObj = null;

        AddDomainEvent(new NodeCreatedDomainEvent(
            NodeId: Id,
            ProcessId: ProcessId,
            TokenId: TokenId,
            ElementId: ElementId,
            OccurredAtUtc: CreatedAtUtc,
            ScopeId: ScopeId,
            ActivityInstanceId: ActivityInstanceId,
            ArrivedViaFlowIds: _arrivedViaFlowIds.ToArray(),
            IsExecutable: IsExecutable
        ));
    }

    public void MarkNonExecutable(string? reason = null)
    {
        if (!IsExecutable) return;

        IsExecutable = false;
        WorkerId = null;
        UserTaskId = null;
    }

    public void Start()
    {
        if (!IsExecutable) return;
        if (State != NodeState.Created) return;

        State = NodeState.Processing;
        StartedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new NodeStartedDomainEvent(
            NodeId: Id,
            ProcessId: ProcessId,
            TokenId: TokenId,
            ElementId: ElementId,
            OccurredAtUtc: StartedAtUtc.Value
        ));
    }

    public void WaitForWorker(Guid workerId, string? reason = null)
    {
        if (!IsExecutable) return;
        if (workerId == Guid.Empty) throw new ArgumentException("WorkerId cannot be empty.", nameof(workerId));
        if (State is NodeState.Completed or NodeState.Failed or NodeState.Skipped) return;

        if (State == NodeState.Created)
            Start();

        WorkerId = workerId;
        UserTaskId = null;

        State = NodeState.Waiting;

        AddDomainEvent(new NodeWaitingDomainEvent(
            NodeId: Id,
            ProcessId: ProcessId,
            TokenId: TokenId,
            ElementId: ElementId,
            WorkerId: workerId,
            UserTaskId: null,
            Reason: string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            OccurredAtUtc: DateTime.UtcNow
        ));
    }

    public void WaitForUserTask(Guid userTaskId, Guid? workerId = null, string? reason = null)
    {
        if (!IsExecutable) return;
        if (userTaskId == Guid.Empty) throw new ArgumentException("UserTaskId cannot be empty.", nameof(userTaskId));
        if (State is NodeState.Completed or NodeState.Failed or NodeState.Skipped) return;

        if (State == NodeState.Created)
            Start();

        UserTaskId = userTaskId;
        WorkerId = workerId is { } w && w != Guid.Empty ? w : null;

        State = NodeState.Waiting;

        AddDomainEvent(new NodeWaitingDomainEvent(
            NodeId: Id,
            ProcessId: ProcessId,
            TokenId: TokenId,
            ElementId: ElementId,
            WorkerId: WorkerId,
            UserTaskId: userTaskId,
            Reason: string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            OccurredAtUtc: DateTime.UtcNow
        ));
    }

    public void ResumeFromWait(string? reason = null)
    {
        if (!IsExecutable) return;
        if (State != NodeState.Waiting) return;

        var oldWorkerId = WorkerId;
        WorkerId = null;
        UserTaskId = null;

        State = NodeState.Processing;

        AddDomainEvent(new NodeResumedDomainEvent(
            NodeId: Id,
            ProcessId: ProcessId,
            TokenId: TokenId,
            ElementId: ElementId,
            PreviousWorkerId: oldWorkerId,
            Reason: string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            OccurredAtUtc: DateTime.UtcNow
        ));
    }

    public void Complete()
    {
        if (State is NodeState.Completed or NodeState.Failed or NodeState.Skipped) return;

        if (State == NodeState.Created)
            Start();

        State = NodeState.Completed;
        CompletedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new NodeCompletedDomainEvent(
            NodeId: Id,
            ProcessId: ProcessId,
            TokenId: TokenId,
            ElementId: ElementId,
            OccurredAtUtc: CompletedAtUtc.Value
        ));
    }

    public void Fail(string errorMessage)
    {
        if (!IsExecutable) return;
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("ErrorMessage is required.", nameof(errorMessage));

        if (State is NodeState.Completed or NodeState.Failed or NodeState.Skipped) return;

        ErrorMessage = errorMessage.Trim();
        State = NodeState.Failed;
        CompletedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new NodeFailedDomainEvent(
            NodeId: Id,
            ProcessId: ProcessId,
            TokenId: TokenId,
            ElementId: ElementId,
            ErrorMessage: ErrorMessage,
            OccurredAtUtc: CompletedAtUtc.Value
        ));
    }

    public void Skip(string? reason = null)
    {
        if (!IsExecutable) return;
        if (State is NodeState.Completed or NodeState.Failed or NodeState.Skipped) return;

        State = NodeState.Skipped;
        CompletedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new NodeSkippedDomainEvent(
            NodeId: Id,
            ProcessId: ProcessId,
            TokenId: TokenId,
            ElementId: ElementId,
            Reason: string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            OccurredAtUtc: CompletedAtUtc.Value
        ));
    }

    public void WaitForJoin()
    {
        if (!IsExecutable) return;
        if (State is NodeState.Completed or NodeState.Failed or NodeState.Skipped) return;

        State = NodeState.Waiting;
    }

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

    /// <summary>
    /// Upsert variable (JSON null is allowed; it does NOT remove the key).
    /// </summary>
    public void SetVariable(string key, object? value)
    {
        if (!IsExecutable) return;
        if (State is NodeState.Completed or NodeState.Failed or NodeState.Skipped) return;

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

        AddDomainEvent(new NodeVariableSetDomainEvent(
            NodeId: Id,
            ProcessId: ProcessId,
            TokenId: TokenId,
            ElementId: ElementId,
            Key: k,
            Value: newJson,
            OccurredAtUtc: DateTime.UtcNow
        ));
    }

    /// <summary>
    /// Remove variable key completely.
    /// </summary>
    public void RemoveVariable(string key)
    {
        if (!IsExecutable) return;
        if (State is NodeState.Completed or NodeState.Failed or NodeState.Skipped) return;

        if (string.IsNullOrWhiteSpace(key)) return;
        var k = NormalizeKey(key);

        var vars = Vars();
        if (!vars.Remove(k))
            return;

        FlushVars(vars);

        AddDomainEvent(new NodeVariableRemovedDomainEvent(
            NodeId: Id,
            ProcessId: ProcessId,
            TokenId: TokenId,
            ElementId: ElementId,
            Key: k,
            OccurredAtUtc: DateTime.UtcNow
        ));
    }
    public void ApplyVariablesPatch(IReadOnlyDictionary<string, JsonNode?> rawTokenVars)
    {
        if (!IsExecutable) return;
        if (rawTokenVars is null || rawTokenVars.Count == 0) return;

        var patch = VariablesPatch.UpsertAllFromNodes(rawTokenVars);
        ApplyVariablesPatch(patch);
    }
    public void ApplyVariablesPatch(VariablesPatch patch)
    {
        if (!IsExecutable) return;
        if (State is NodeState.Completed or NodeState.Failed or NodeState.Skipped) return;

        if (patch is null) throw new ArgumentNullException(nameof(patch));
        if (!patch.HasChanges) return;

        var vars = Vars();

        // removals
        foreach (var raw in patch.Removals ?? Array.Empty<string>())
        {
            var k = NormalizeKey(raw);
            if (k.Length == 0) continue;
            vars.Remove(k);
        }

        // upserts (already JsonNode?)
        foreach (var kv in patch.Upserts ?? new Dictionary<string, JsonNode?>(StringComparer.Ordinal))
        {
            var k = NormalizeKey(kv.Key);
            if (k.Length == 0) continue;
            vars[k] = kv.Value;
        }

        FlushVars(vars);
    }

    // ---- internals ----

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

    private static string NormalizeKey(string key) => key.Trim();

    private static string EnsureKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key is required.", nameof(key));
        return key.Trim();
    }
}



// =======================
// Domain Events (one-file)
// =======================

public sealed record NodeCreatedDomainEvent(
    Guid NodeId,
    Guid ProcessId,
    Guid TokenId,
    string ElementId,
    DateTime OccurredAtUtc,
    Guid? ScopeId,
    Guid? ActivityInstanceId,
    string[] ArrivedViaFlowIds,
    bool IsExecutable
) : IDomainEvent;

public sealed record NodeStartedDomainEvent(
    Guid NodeId,
    Guid ProcessId,
    Guid TokenId,
    string ElementId,
    DateTime OccurredAtUtc
) : IDomainEvent;

public sealed record NodeWaitingDomainEvent(
    Guid NodeId,
    Guid ProcessId,
    Guid TokenId,
    string ElementId,
    Guid? WorkerId,
    Guid? UserTaskId,
    string? Reason,
    DateTime OccurredAtUtc
) : IDomainEvent;

public sealed record NodeResumedDomainEvent(
    Guid NodeId,
    Guid ProcessId,
    Guid TokenId,
    string ElementId,
    Guid? PreviousWorkerId,
    string? Reason,
    DateTime OccurredAtUtc
) : IDomainEvent;

public sealed record NodeCompletedDomainEvent(
    Guid NodeId,
    Guid ProcessId,
    Guid TokenId,
    string ElementId,
    DateTime OccurredAtUtc
) : IDomainEvent;

public sealed record NodeFailedDomainEvent(
    Guid NodeId,
    Guid ProcessId,
    Guid TokenId,
    string ElementId,
    string ErrorMessage,
    DateTime OccurredAtUtc
) : IDomainEvent;

public sealed record NodeSkippedDomainEvent(
    Guid NodeId,
    Guid ProcessId,
    Guid TokenId,
    string ElementId,
    string? Reason,
    DateTime OccurredAtUtc
) : IDomainEvent;

public sealed record NodeVariableSetDomainEvent(
    Guid NodeId,
    Guid ProcessId,
    Guid TokenId,
    string ElementId,
    string Key,
    string Value,
    DateTime OccurredAtUtc
) : IDomainEvent;

public sealed record NodeVariableRemovedDomainEvent(
    Guid NodeId,
    Guid ProcessId,
    Guid TokenId,
    string ElementId,
    string Key,
    DateTime OccurredAtUtc
) : IDomainEvent;
