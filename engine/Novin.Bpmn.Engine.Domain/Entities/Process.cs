using System;
using System.Collections.Generic;
using System.Linq;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Domain.Entities;

public sealed class Process : BaseAggregateRoot
{
    public Guid ProjectId { get; private set; }          // ✅ اتصال به پروژه
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

    // ---- IDs only (DDD-safe) ----
    private readonly HashSet<Guid> _tokenIds = new();
    public IReadOnlyCollection<Guid> TokenIds => _tokenIds;

    private readonly HashSet<Guid> _nodeInstanceIds = new();   // ✅ جایگزین ExecutedNode
    public IReadOnlyCollection<Guid> NodeInstanceIds => _nodeInstanceIds;

    // ---- Variables ----
    private readonly Dictionary<string,string> _variables = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string,string> Variables => _variables;

    private Process()
    {
        State = ProcessState.Created;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Process Create(
        Guid projectId,
        Guid deploymentId,
        string processBpmnId,
        string name,
        IDictionary<string, object?>? initialVariables = null,
        string? businessKey = null)
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

        if (initialVariables is not null)
        {
            var patch = ProcessVariablesPatch.From(initialVariables, Enumerable.Empty<string>());
            foreach (var kv in patch.Upserts)
                p._variables[kv.Key] = kv.Value;
        }

        p.AddDomainEvent(new ProcessInstanceCreatedEvent(
            p.Id, p.ProjectId, p.DeploymentId, p.ProcessBpmnId, p.BusinessKey,
            new Dictionary<string,string>(p._variables), p.CreatedAtUtc));

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

        // completion decision should be evaluated by a handler:
        // - no active executable tokens
        // - no pending workers
        // so Process itself doesn't scan DB here.

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

    // ---- Variables ----
    public void ApplyVariablesPatch(ProcessVariablesPatch patch)
    {
        if (patch is null) throw new ArgumentNullException(nameof(patch));
        if (!patch.HasChanges) return;

        EnsureCanAcceptRuntimeMutations();

        foreach (var upsert in patch.Upserts)
            _variables[upsert.Key] = upsert.Value;

        foreach (var removal in patch.Removals)
            _variables.Remove(removal);

        AddDomainEvent(new ProcessVariablesChangedEvent(
            Id,
            new Dictionary<string,string>(patch.Upserts),
            patch.Removals.ToList().AsReadOnly(),
            DateTime.UtcNow));
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
