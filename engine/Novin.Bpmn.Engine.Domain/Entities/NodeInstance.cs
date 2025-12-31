using System.Text.Json;
using Novin.Bpmn.Engine.Domain.Common;

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

public sealed class NodeInstance : BaseAggregateRoot
{
    public Guid ProcessId { get; private set; }
    public Guid TokenId { get; private set; }

    /// <summary>BPMN element id (e.g., "UserTask_1")</summary>
    public string ElementId { get; private set; } = default!;

    public NodeState State { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public Guid? ScopeId { get; private set; }
    public Guid? ActivityInstanceId { get; private set; }
    public string? ArrivedViaFlowId { get; private set; }

    /// <summary>Job correlation if this node waits for external/user completion</summary>
    public Guid? WorkerId { get; private set; }
    public Guid? UserTaskId { get; private set; }

    /// <summary>Failure detail (revealed to ops/UI if needed)</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Execution flag (mirrors Token.IsExecutable intent).
    /// If false => aggregate ignores all transitions/changes (no auto-skip).
    /// </summary>
    public bool IsExecutable { get; private set; }

    private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, string> Variables => _variables;

    private NodeInstance() { } // EF

    public NodeInstance(
        Guid processId,
        Guid tokenId,
        string elementId,
        Guid? scopeId,
        Guid? activityInstanceId,
        string? arrivedViaFlowId,
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
        ArrivedViaFlowId = string.IsNullOrWhiteSpace(arrivedViaFlowId) ? null : arrivedViaFlowId.Trim();

        IsExecutable = isExecutable;

        State = NodeState.Created;
        CreatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new NodeCreatedDomainEvent(
            NodeId: Id,
            ProcessId: ProcessId,
            TokenId: TokenId,
            ElementId: ElementId,
            OccurredAtUtc: CreatedAtUtc,
            ScopeId: ScopeId,
            ActivityInstanceId: ActivityInstanceId,
            ArrivedViaFlowId: ArrivedViaFlowId,
            IsExecutable: IsExecutable
        ));
    }

    /// <summary>
    /// Make this node non-executable. Does NOT auto-skip and does NOT change State.
    /// After this, all operations become no-ops.
    /// </summary>
    public void MarkNonExecutable(string? reason = null)
    {
        if (!IsExecutable) return;

        IsExecutable = false;

        // Optional cleanup: avoid stale correlation
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
        if (!IsExecutable) return; // <- important: non-executable does not even generate skipped
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

    public void SetVariable(string key, object? value)
    {
        if (!IsExecutable) return;

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key is required.", nameof(key));

        key = key.Trim();

        if (value is null)
        {
            if (_variables.Remove(key))
                AddDomainEvent(new NodeVariableRemovedDomainEvent(
                    NodeId: Id,
                    ProcessId: ProcessId,
                    TokenId: TokenId,
                    ElementId: ElementId,
                    Key: key,
                    OccurredAtUtc: DateTime.UtcNow
                ));

            return;
        }

        var serialized = SerializeValue(value);
        _variables[key] = serialized;

        AddDomainEvent(new NodeVariableSetDomainEvent(
            NodeId: Id,
            ProcessId: ProcessId,
            TokenId: TokenId,
            ElementId: ElementId,
            Key: key,
            Value: serialized,
            OccurredAtUtc: DateTime.UtcNow
        ));
    }

    public bool TryGetVariable(string key, out string? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(key)) return false;
        return _variables.TryGetValue(key.Trim(), out value);
    }

    private static string SerializeValue(object value)
    {
        if (value is string s) return s;

        return JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    public void WaitForJoin()
    {
        State = NodeState.Waiting;
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
    string? ArrivedViaFlowId,
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
