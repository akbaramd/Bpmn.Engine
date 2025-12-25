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
    public string ProcessDefinitionId { get; private set; }
    public ProcessState State { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    // Tokens belong to the process (IDs only – DDD safe)
    private readonly HashSet<Guid> _tokenIds = new();
    public IReadOnlyCollection<Guid> TokenIds => _tokenIds;

    // Variables for dynamic state
    private readonly Dictionary<string, object> _variables = new();
    public IReadOnlyDictionary<string, object> Variables => _variables;

    private Process()
    {
        State = ProcessState.Created;
        CreatedAt = DateTime.UtcNow;
    }

    public Process(
        string name,
        string processDefinitionId, Dictionary<string,object>? init) : this()
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Process name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(processDefinitionId))
            throw new ArgumentException("Process definition id cannot be empty", nameof(processDefinitionId));

        Name = name;
        ProcessDefinitionId = processDefinitionId;
        if (init != null)
            foreach (var kv in init)
                _variables[kv.Key] = kv.Value;

        AddDomainEvent(new ProcessCreatedEvent(
            Id,
            Name,
            ProcessDefinitionId,
            CreatedAt));
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

    public void Suspend()
    {
        EnsureState(ProcessState.Running);

        State = ProcessState.Suspended;
        AddDomainEvent(new ProcessSuspendedEvent(Id, DateTime.UtcNow));
    }

    public void Resume()
    {
        EnsureState(ProcessState.Suspended);

        State = ProcessState.Running;
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
        AddDomainEvent(new ProcessTerminatedEvent(Id, DateTime.UtcNow, reason));
    }

    public void Fail(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be empty", nameof(errorMessage));

        State = ProcessState.Failed;
        AddDomainEvent(new ProcessFailedEvent(Id, DateTime.UtcNow, errorMessage));
    }

    // -------------------- Token Ownership --------------------

    public void AddToken(Guid tokenId)
    {
        // ⚠️ Allow adding tokens if:
        // 1. Process is Running (normal case)
        // 2. Process is Completed but still has tokens (race condition: completion happened too early)
        //    This can happen when a token is being processed while completion evaluation runs
        //    In this case, we resume the process from Completed to Running
        if (State is not ProcessState.Running)
        {
            // If process is Completed but still has tokens, resume it and allow adding new tokens
            // This handles race conditions where completion evaluation runs while tokens are still processing
            if (State == ProcessState.Completed && _tokenIds.Count > 0)
            {
                // Resume the process - this will change state back to Running
                ResumeFromCompleted();
            }
            else
            {
                EnsureState(ProcessState.Running);
            }
        }

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

    /// <summary>
    /// Set a variable for the process
    /// </summary>
    public void SetVariable(string name, object value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Variable name cannot be null or empty", nameof(name));

        _variables[name] = value;

        AddToHistory($"Variable {name} set to {value}");
    }

    /// <summary>
    /// Get a variable for the process
    /// </summary>
    public object GetVariable(string name)
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

    // -------------------- History --------------------

    private void AddToHistory(string entry)
    {
        // You can implement a proper history mechanism here
        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {entry}");
    }
}
