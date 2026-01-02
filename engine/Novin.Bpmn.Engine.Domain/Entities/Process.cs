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

    private readonly HashSet<Guid> _nodeInstanceIds = new();   // ✅ جایگزین NodeInstance
    public IReadOnlyCollection<Guid> NodeInstanceIds => _nodeInstanceIds;

    // ---- Variables ----
    private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, string> Variables => _variables;


    // ---- Metadata ----
    private readonly Dictionary<string, string> _metadata = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, string> Metadata => _metadata;

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
        string? businessKey = null,
         IDictionary<string, object?>? initialMetadata = null)
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

        // ✅ Metadata (new) - store as string
        if (initialMetadata is not null)
        {
            var patch = ProcessMetadataPatch.From(initialMetadata, Enumerable.Empty<string>());
            foreach (var kv in patch.Upserts)
                p._metadata[kv.Key] = kv.Value;
        }

        p.AddDomainEvent(new ProcessInstanceCreatedEvent(
            p.Id, p.ProjectId, p.DeploymentId, p.ProcessBpmnId, p.BusinessKey,
            new Dictionary<string, string>(p._variables), p.CreatedAtUtc));

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
            new Dictionary<string, string>(patch.Upserts),
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

    // داخل کلاس Process

    public void SetVariable(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Variable key cannot be empty.", nameof(key));

        EnsureCanAcceptRuntimeMutations();

        key = key.Trim();

        // Convention: empty => remove
        if (string.IsNullOrWhiteSpace(value))
        {
            if (_variables.Remove(key))
            {
                AddDomainEvent(new ProcessVariablesChangedEvent(
                    Id,
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    new List<string> { key }.AsReadOnly(),
                    DateTime.UtcNow));
            }
            return;
        }

        var newValue = value; // already string

        // Idempotent upsert: if unchanged => no event
        if (_variables.TryGetValue(key, out var oldValue) &&
            string.Equals(oldValue, newValue, StringComparison.Ordinal))
            return;

        _variables[key] = newValue;

        AddDomainEvent(new ProcessVariablesChangedEvent(
            Id,
            new Dictionary<string, string>(StringComparer.Ordinal) { [key] = newValue },
            Array.Empty<string>(),
            DateTime.UtcNow));
    }

    public bool HasVariable(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return _variables.ContainsKey(key.Trim());
    }

    public string? GetVariable(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        key = key.Trim();
        return _variables.TryGetValue(key, out var v) ? v : null;
    }

    // Optional but useful
    public bool TryGetVariable(string key, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return _variables.TryGetValue(key.Trim(), out value!);
    }

    public void RemoveVariable(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        EnsureCanAcceptRuntimeMutations();

        key = key.Trim();
        if (!_variables.Remove(key))
            return;

        AddDomainEvent(new ProcessVariablesChangedEvent(
            Id,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new List<string> { key }.AsReadOnly(),
            DateTime.UtcNow));
    }

    public void SetMetadata(string key, string value)
{
    if (string.IsNullOrWhiteSpace(key))
        throw new ArgumentException("Metadata key cannot be empty.", nameof(key));

    EnsureCanAcceptRuntimeMutations();

    key = key.Trim();

    // Convention: empty => remove
    if (string.IsNullOrWhiteSpace(value))
    {
        if (_metadata.Remove(key))
        {
            AddDomainEvent(new ProcessMetadataChangedEvent(
                Id,
                new Dictionary<string, string>(StringComparer.Ordinal),
                new List<string> { key }.AsReadOnly(),
                DateTime.UtcNow));
        }
        return;
    }

    var newValue = value;

    // Idempotent upsert
    if (_metadata.TryGetValue(key, out var oldValue) &&
        string.Equals(oldValue, newValue, StringComparison.Ordinal))
        return;

    _metadata[key] = newValue;

    AddDomainEvent(new ProcessMetadataChangedEvent(
        Id,
        new Dictionary<string, string>(StringComparer.Ordinal) { [key] = newValue },
        Array.Empty<string>(),
        DateTime.UtcNow));
}

public bool HasMetadata(string key)
{
    if (string.IsNullOrWhiteSpace(key))
        return false;

    return _metadata.ContainsKey(key.Trim());
}

public string? GetMetadata(string key)
{
    if (string.IsNullOrWhiteSpace(key))
        return null;

    key = key.Trim();
    return _metadata.TryGetValue(key, out var v) ? v : null;
}

public bool TryGetMetadata(string key, out string value)
{
    value = string.Empty;
    if (string.IsNullOrWhiteSpace(key))
        return false;

    return _metadata.TryGetValue(key.Trim(), out value!);
}

public void RemoveMetadata(string key)
{
    if (string.IsNullOrWhiteSpace(key))
        return;

    EnsureCanAcceptRuntimeMutations();

    key = key.Trim();
    if (!_metadata.Remove(key))
        return;

    AddDomainEvent(new ProcessMetadataChangedEvent(
        Id,
        new Dictionary<string, string>(StringComparer.Ordinal),
        new List<string> { key }.AsReadOnly(),
        DateTime.UtcNow));
}

public void ApplyMetadataPatch(ProcessMetadataPatch patch)
{
    if (patch is null) throw new ArgumentNullException(nameof(patch));
    if (!patch.HasChanges) return;

    EnsureCanAcceptRuntimeMutations();

    foreach (var upsert in patch.Upserts)
        _metadata[upsert.Key] = upsert.Value;

    foreach (var removal in patch.Removals)
        _metadata.Remove(removal);

    AddDomainEvent(new ProcessMetadataChangedEvent(
        Id,
        new Dictionary<string, string>(patch.Upserts),
        patch.Removals.ToList().AsReadOnly(),
        DateTime.UtcNow));
}
}
