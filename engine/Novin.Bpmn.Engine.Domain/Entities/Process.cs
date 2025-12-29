using System;
using System.Collections.Generic;
using System.Linq;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Domain.Entities;

/// <summary>
/// Aggregate root representing a BPMN process instance
/// (token-driven, history-free, engine-safe)
/// </summary>
public sealed class Process : BaseAggregateRoot
{
    public string Name { get; private set; }

    /// <summary>
    /// ID of the deployment this process instance is running on
    /// </summary>
    public Guid DeploymentId { get; private set; }

    /// <summary>
    /// BPMN Process ID within the deployment (the actual process identifier in BPMN XML)
    /// </summary>
    public string ProcessBpmnId { get; private set; }
    public string? BusinessKey { get; private set; }

    public ProcessState State { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? SuspendedAt { get; private set; }
    public DateTime? FailedAt { get; private set; }
    public DateTime? TerminatedAt { get; private set; }
    public string? FailureReason { get; private set; }
    public string? TerminationReason { get; private set; }
    
    public string ProcessDefinitionId => ProcessBpmnId;

    // Tokens belong to the process (IDs only – DDD safe)
    private readonly HashSet<Guid> _tokenIds = new();
    public IReadOnlyCollection<Guid> TokenIds => _tokenIds;

    // Variables for dynamic state - stored as strings
    private readonly Dictionary<string, string> _variables = new();
    public IReadOnlyDictionary<string, string> Variables => _variables;

    private Process()
    {
        State = ProcessState.Created;
        CreatedAt = DateTime.UtcNow;
    }

    public static Process Create(
        string name,
        Guid deploymentId,
        string processBpmnId,
        IDictionary<string, object?>? initialVariables = null,
        string? businessKey = null)
    {
        var process = new Process();
        process.Initialize(name, deploymentId, processBpmnId, businessKey, initialVariables);
        return process;
    }

    private void Initialize(
        string name,
        Guid deploymentId,
        string processBpmnId,
        string? businessKey,
        IDictionary<string, object?>? initialVariables)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Process name cannot be empty", nameof(name));

        if (deploymentId == Guid.Empty)
            throw new ArgumentException("Deployment ID cannot be empty", nameof(deploymentId));

        if (string.IsNullOrWhiteSpace(processBpmnId))
            throw new ArgumentException("Process BPMN ID cannot be empty", nameof(processBpmnId));

        Name = name;
        DeploymentId = deploymentId;
        ProcessBpmnId = processBpmnId;
        BusinessKey = string.IsNullOrWhiteSpace(businessKey) ? null : businessKey;

        if (initialVariables is not null)
        {
            var patch = ProcessVariablesPatch.From(initialVariables, Enumerable.Empty<string>());
            foreach (var kv in patch.Upserts)
            {
                _variables[kv.Key] = kv.Value;
            }
        }

        AddDomainEvent(new ProcessInstanceCreatedEvent(
            Id,
            DeploymentId,
            ProcessBpmnId,
            BusinessKey,
            new Dictionary<string, string>(_variables),
            CreatedAt));
    }

    public Process(
        string name,
        Guid deploymentId,
        string processBpmnId,
        Dictionary<string, string>? init = null) : this()
    {
        var variables = init?.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
        Initialize(name, deploymentId, processBpmnId, businessKey: null, initialVariables: variables);
    }

    // -------------------- Lifecycle --------------------

    public void Start()
    {
        EnsureState(ProcessState.Created);

        State = ProcessState.Running;
        StartedAt = DateTime.UtcNow;

        AddDomainEvent(new ProcessStartedEvent(Id, StartedAt.Value));
    }

    public void Complete()
    {
        EnsureState(ProcessState.Running);

        // Note: Guard removed - ProcessCompletionEvaluator ensures no live tokens exist
        // This allows completion evaluation to be done asynchronously via events

        State = ProcessState.Completed;
        CompletedAt = DateTime.UtcNow;

        AddDomainEvent(new ProcessCompletedEvent(Id, CompletedAt.Value));
    }

    public void Suspend(string? reason = null)
    {
        EnsureState(ProcessState.Running);

        State = ProcessState.Suspended;
        SuspendedAt = DateTime.UtcNow;
        AddDomainEvent(new ProcessSuspendedEvent(Id, SuspendedAt.Value, reason));
    }

    public void Resume()
    {
        EnsureState(ProcessState.Suspended);

        State = ProcessState.Running;
        SuspendedAt = null;
        AddDomainEvent(new ProcessResumedEvent(Id, DateTime.UtcNow));
    }

    /// <summary>
    /// Resumes a process from Completed state back to Running.
    /// This handles race conditions where completion evaluation runs while tokens are still processing.
    /// </summary>
    public void ResumeFromCompleted()
    {
        if (State != ProcessState.Completed)
            throw new InvalidOperationException(
                $"Cannot resume from Completed state when process is in {State} state.");

        State = ProcessState.Running;
        CompletedAt = null;
        AddDomainEvent(new ProcessResumedEvent(Id, DateTime.UtcNow));
    }

    public void Terminate(string? reason = null)
    {
        if (State is ProcessState.Completed or ProcessState.Terminated)
            throw new InvalidOperationException(
                $"Cannot terminate process in {State} state.");

        State = ProcessState.Terminated;
        TerminatedAt = DateTime.UtcNow;
        TerminationReason = reason;
        AddDomainEvent(new ProcessTerminatedEvent(Id, TerminatedAt.Value, reason));
    }

    public void Fail(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be empty", nameof(errorMessage));

        if (State is ProcessState.Completed or ProcessState.Terminated or ProcessState.Failed)
            throw new InvalidOperationException($"Cannot fail process in {State} state.");

        State = ProcessState.Failed;
        FailedAt = DateTime.UtcNow;
        FailureReason = errorMessage;
        AddDomainEvent(new ProcessFailedEvent(Id, FailedAt.Value, errorMessage));
    }

    /// <summary>
    /// Handles unhandled BPMN error escalation to process level.
    /// Converts all executable tokens to trace tokens and fails the process.
    /// </summary>
    public void HandleUnhandledBpmnError(string errorCode, string errorMessage)
    {
        // This method is called by event handlers when BPMN error cannot be handled by boundary events
        // The actual logic is implemented in the BpmnErrorOccurredEventHandler
        AddDomainEvent(new ProcessFailedEvent(Id, DateTime.UtcNow,
            $"Unhandled BPMN Error: {errorCode} - {errorMessage}"));
    }

    // -------------------- Token Ownership --------------------

    public void AddToken(Guid tokenId)
    {
        EnsureCanAcceptTokens();

        if (tokenId == Guid.Empty)
            throw new ArgumentException("Token id cannot be empty", nameof(tokenId));

        if (!_tokenIds.Add(tokenId))
            throw new InvalidOperationException(
                $"Token {tokenId} already exists in process.");
    }

    public void RemoveToken(Guid tokenId)
    {
        if (!_tokenIds.Remove(tokenId))
            throw new InvalidOperationException(
                $"Token {tokenId} does not belong to process.");
    }

    public bool HasActiveTokens() => _tokenIds.Count > 0;

    // -------------------- Variables --------------------

    public void ApplyVariablesPatch(ProcessVariablesPatch patch)
    {
        if (patch is null) throw new ArgumentNullException(nameof(patch));
        if (!patch.HasChanges) return;

        EnsureMutable();

        var appliedUpserts = new Dictionary<string, string>(StringComparer.Ordinal);
        var appliedRemovals = new List<string>();

        foreach (var upsert in patch.Upserts)
        {
            _variables[upsert.Key] = upsert.Value;
            appliedUpserts[upsert.Key] = upsert.Value;
        }

        foreach (var removal in patch.Removals)
        {
            if (_variables.Remove(removal))
            {
                appliedRemovals.Add(removal);
            }
        }

        if (appliedUpserts.Count == 0 && appliedRemovals.Count == 0)
            return;

        AddDomainEvent(new ProcessVariablesChangedEvent(
            Id,
            new Dictionary<string, string>(appliedUpserts),
            appliedRemovals.AsReadOnly(),
            DateTime.UtcNow));
    }

    /// <summary>
    /// Set a variable for the process (converts value to string)
    /// </summary>
    public void SetVariable(string name, object? value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Variable name cannot be null or empty", nameof(name));

        ApplyVariablesPatch(ProcessVariablesPatch.From(
            new Dictionary<string, object?> { { name, value } },
            Enumerable.Empty<string>()));
    }

    /// <summary>
    /// Get a variable for the process (returns as string)
    /// </summary>
    public string GetVariable(string name)
    {
        if (!_variables.ContainsKey(name))
            throw new KeyNotFoundException($"Variable '{name}' not found.");

        return _variables[name];
    }

    /// <summary>
    /// Check if the process has a specific variable
    /// </summary>
    public bool HasVariable(string name)
    {
        return _variables.ContainsKey(name);
    }

    // -------------------- Guards --------------------

    private void EnsureState(ProcessState required)
    {
        if (State != required)
            throw new InvalidOperationException(
                $"Process must be in {required} state but is {State}.");
    }

    private void EnsureCanAcceptTokens()
    {
        if (State is ProcessState.Running)
            return;

        if (State == ProcessState.Completed && _tokenIds.Count > 0)
        {
            ResumeFromCompleted();
            return;
        }

        throw new InvalidOperationException($"Process must be Running to accept tokens. Current state: {State}");
    }

    private void EnsureMutable()
    {
        if (State is ProcessState.Completed or ProcessState.Terminated or ProcessState.Failed)
            throw new InvalidOperationException($"Process in {State} state is immutable.");
    }

    // -------------------- Navigation Properties --------------------

    /// <summary>
    /// Execution path nodes for this process (minimal audit trail)
    /// </summary>
    private readonly List<ExecutedNode> _executionNodes = new();
    public IReadOnlyCollection<ExecutedNode> ExecutionNodes => _executionNodes.AsReadOnly();

    // -------------------- History --------------------

    private void AddToHistory(string entry)
    {
        // You can implement a proper history mechanism here
        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {entry}");
    }
}
