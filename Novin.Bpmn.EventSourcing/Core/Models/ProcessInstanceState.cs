using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Json;
using Novin.Bpmn.EventSourcing.Events;

namespace Novin.Bpmn.EventSourcing.Core.Models
{
    /// <summary>
    /// Optimized, serialization-friendly state of a BPMN process instance.
    /// Tracks variables, executions, subscriptions, jobs, incidents, and event history.
    /// </summary>
    public class ProcessInstanceState
    {
        // Identity & Definition
        public ProcessInstanceState()
        {
        }
        
        // Core identity fields
        public string InstanceId { get; set; } = string.Empty;
        public Guid DeploymentId { get; set; }
        public string DeploymentKey { get; set; } = string.Empty;
        public string ProcessId { get; set; } = string.Empty;
        public int DefinitionVersion { get; set; }

        // Lifecycle & Timing
        public ProcessInstanceStatus Status { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        // Process data
        public Dictionary<string, object> Variables { get; set; } = new();
        
        // Using ConcurrentDictionary for thread-safe execution tracking
        private ConcurrentDictionary<string, ElementExecution> _executions = new();
        
        // This property maintains serialization compatibility
        [JsonInclude]
        public Dictionary<string, ElementExecution> Executions 
        { 
            get => _executions.ToDictionary(k => k.Key, v => v.Value);
            set => _executions = new ConcurrentDictionary<string, ElementExecution>(value); 
        }
        
        // Direct access to ConcurrentDictionary for thread-safe operations
        [JsonIgnore]
        public ConcurrentDictionary<string, ElementExecution> ConcurrentExecutions => _executions;
        
        public List<EventSubscription> Subscriptions { get; set; } = new();
        public List<Job> Jobs { get; set; } = new();
        public List<Incident> Incidents { get; set; } = new();
        public List<SerializableBpmnEvent> History { get; set; } = new();
        
        // Computed properties
        [JsonIgnore]
        public IReadOnlyCollection<ElementExecution> ActiveExecutions =>
            _executions.Values
                .Where(e => e.Status == ExecutionStatus.Active 
                         || e.Status == ExecutionStatus.Waiting 
                         || e.Status == ExecutionStatus.Suspended)
                .ToList()
                .AsReadOnly();

        [JsonIgnore]
        public IReadOnlyCollection<ElementExecution> CompletedExecutions =>
            _executions.Values
                .Where(e => e.Status == ExecutionStatus.Completed 
                         || e.Status == ExecutionStatus.Failed 
                         || e.Status == ExecutionStatus.Terminated)
                .ToList()
                .AsReadOnly();
                
        [JsonIgnore]
        public bool IsActive => Status == ProcessInstanceStatus.Active || Status == ProcessInstanceStatus.Waiting;
        
        [JsonIgnore]
        public bool IsCompleted => Status == ProcessInstanceStatus.Completed;
        
        [JsonIgnore]
        public bool IsFailed => Status == ProcessInstanceStatus.Failed;
        
        [JsonIgnore]
        public bool IsTerminated => Status == ProcessInstanceStatus.Terminated || Status == ProcessInstanceStatus.Cancelled;

        /// <summary>
        /// Bootstrap a new instance from ProcessStarted.
        /// </summary>
        public static ProcessInstanceState From(ProcessStarted evt)
        {
            if (evt is null) throw new ArgumentNullException(nameof(evt));
            var now = evt.Timestamp;
            var state = new ProcessInstanceState
            {
                InstanceId = evt.InstanceId,
                DeploymentId = evt.DeploymentId,
                DeploymentKey = evt.DeploymentKey,
                DefinitionVersion = evt.DefinitionVersion,
                Status = ProcessInstanceStatus.Active,
                StartedAt = now,
                LastUpdatedAt = now
            };
            state.RecordEvent(evt);
            return state;
        }

        // — Variables —

        public void SetVariable(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Variable name required", nameof(name));

            Variables[name] = value;
            Touch();
        }

        public bool RemoveVariable(string name)
        {
            var removed = Variables.Remove(name);
            if (removed) Touch();
            return removed;
        }

        public bool TryGetVariable<T>(string name, out T value)
        {
            if (Variables.TryGetValue(name, out var obj) && obj is T cast)
            {
                value = cast;
                return true;
            }
            value = default!;
            return false;
        }

        // — Executions —

        public void AddExecution(ElementExecution exec)
        {
            if (exec is null) throw new ArgumentNullException(nameof(exec));
            _executions[exec.ExecutionId] = exec;
            Touch();
        }

        public ElementExecution GetExecution(string executionId)
        {
            if (!_executions.TryGetValue(executionId, out var exec))
                throw new KeyNotFoundException($"Execution '{executionId}' not found.");
            return exec;
        }

        public void CompleteExecution(string executionId)
        {
            var exec = GetExecution(executionId);
            exec.Complete();
            
            // Sync any local variables from the execution to the process instance
            SyncVariablesFromExecution(exec);
            
            Touch();
        }
        
        /// <summary>
        /// Updates the process variables with any local variables from the specified execution.
        /// This helps propagate execution-scoped variables up to the process level.
        /// </summary>
        public void SyncVariablesFromExecution(ElementExecution execution)
        {
            if (execution?.LocalVariables == null || !execution.LocalVariables.Any())
                return;
                
            foreach (var kvp in execution.LocalVariables)
            {
                // Copy the variable to the process instance
                Variables[kvp.Key] = kvp.Value;
            }
        }
        
        /// <summary>
        /// Sets a variable in both the process state and the specified execution.
        /// </summary>
        public void SetExecutionVariable(string executionId, string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Variable name required", nameof(name));
                
            // Set at process level
            Variables[name] = value;
            
            // If the execution exists, set it there too
            if (_executions.TryGetValue(executionId, out var exec))
            {
                exec.SetVariable(name, value);
            }
            
            Touch();
        }

        // — Subscriptions —

        public void AddSubscription(EventSubscription sub)
        {
            if (sub is null) throw new ArgumentNullException(nameof(sub));
            Subscriptions.Add(sub);
            Touch();
        }

        public bool RemoveSubscription(string subscriptionId)
        {
            var sub = Subscriptions.FirstOrDefault(s => s.SubscriptionId == subscriptionId);
            if (sub == null) return false;
            Subscriptions.Remove(sub);
            Touch();
            return true;
        }

        // — Jobs —

        public void AddJob(Job job)
        {
            if (job is null) throw new ArgumentNullException(nameof(job));
            Jobs.Add(job);
            Touch();
        }

        public bool RemoveJob(string jobId)
        {
            var j = Jobs.FirstOrDefault(x => x.JobId == jobId);
            if (j == null) return false;
            Jobs.Remove(j);
            Touch();
            return true;
        }

        // — Incidents —

        public void AddIncident(Incident incident)
        {
            if (incident is null) throw new ArgumentNullException(nameof(incident));
            Incidents.Add(incident);
            Touch();
        }

        public bool ResolveIncident(string incidentId)
        {
            var inc = Incidents.FirstOrDefault(x => x.IncidentId == incidentId);
            if (inc == null) return false;
            inc.Resolved = true;
            inc.ResolvedAt = DateTime.UtcNow;
            Touch();
            return true;
        }

        // — Event history —

        public void RecordEvent(IBpmnEvent evt)
        {
            if (evt is null) throw new ArgumentNullException(nameof(evt));
            
            // Convert to SerializableBpmnEvent
            var serializableEvent = SerializableBpmnEvent.FromEvent(evt);
            History.Add(serializableEvent);
            
            Touch();
        }
        
        /// <summary>
        /// Add an event directly to the process history without conversion.
        /// </summary>
        public void RecordSerializableEvent(SerializableBpmnEvent evt)
        {
            if (evt is null) throw new ArgumentNullException(nameof(evt));
            History.Add(evt);
            Touch();
        }
        
        /// <summary>
        /// Gets the last event of a specific type from the history.
        /// </summary>
        public SerializableBpmnEvent GetLastEventOfType(string eventType)
        {
            return History
                .Where(e => e.EventType == eventType)
                .OrderByDescending(e => e.Timestamp)
                .FirstOrDefault();
        }
        
        /// <summary>
        /// Gets all events of a specific type from the history.
        /// </summary>
        public IEnumerable<SerializableBpmnEvent> GetEventsOfType(string eventType)
        {
            return History
                .Where(e => e.EventType == eventType)
                .OrderBy(e => e.Timestamp);
        }
        
        /// <summary>
        /// Gets all events for a specific element from the history.
        /// </summary>
        public IEnumerable<SerializableBpmnEvent> GetEventsForElement(string elementId)
        {
            return History
                .Where(e => e.ElementId == elementId)
                .OrderBy(e => e.Timestamp);
        }

        // — Process lifecycle transitions —

        public void Complete(ProcessCompleted evt)
        {
            if (evt is null) throw new ArgumentNullException(nameof(evt));
            if (Status.IsTerminal) throw new InvalidOperationException("Already in terminal state.");
            Status = ProcessInstanceStatus.Completed;
            CompletedAt = evt.Timestamp;
            RecordEvent(evt);
        }

        public void Fail(ProcessFailed evt)
        {
            if (evt is null) throw new ArgumentNullException(nameof(evt));
            if (Status.IsTerminal) throw new InvalidOperationException("Already in terminal state.");
            Status = ProcessInstanceStatus.Failed;
            CompletedAt = evt.Timestamp;
            RecordEvent(evt);
        }

        public void Terminate(ProcessTerminated evt)
        {
            if (evt is null) throw new ArgumentNullException(nameof(evt));
            if (Status.IsTerminal) throw new InvalidOperationException("Already in terminal state.");
            Status = ProcessInstanceStatus.Terminated;
            CompletedAt = evt.Timestamp;
            RecordEvent(evt);
        }

        public void Suspend(ProcessSuspended evt)
        {
            if (evt is null) throw new ArgumentNullException(nameof(evt));
            if (!Status.CanSuspend) throw new InvalidOperationException($"Cannot suspend from {Status}.");
            Status = ProcessInstanceStatus.Suspended;
            RecordEvent(evt);
        }

        public void Resume(ProcessResumed evt)
        {
            if (evt is null) throw new ArgumentNullException(nameof(evt));
            if (!Status.CanResume) throw new InvalidOperationException($"Cannot resume from {Status}.");
            Status = ProcessInstanceStatus.Active;
            RecordEvent(evt);
        }

        public void Cancel(ProcessCancelled evt)
        {
            if (evt is null) throw new ArgumentNullException(nameof(evt));
            if (Status.IsTerminal) throw new InvalidOperationException("Already in terminal state.");
            Status = ProcessInstanceStatus.Cancelled;
            CompletedAt = evt.Timestamp;
            RecordEvent(evt);
        }

        public void Restart()
        {
           Status = ProcessInstanceStatus.Active;
           CompletedAt = null;
           Touch();
        }
        
        public void Touch() =>
            LastUpdatedAt = DateTime.UtcNow;

        /// <summary>
        /// Merges another execution into this state, handling any potential conflicts.
        /// </summary>
        public void MergeExecution(ElementExecution execution)
        {
            if (execution == null) throw new ArgumentNullException(nameof(execution));
            
            // Use ConcurrentDictionary's built-in thread-safe update method
            _executions.AddOrUpdate(
                execution.ExecutionId,
                // If key doesn't exist, use the new execution
                addValue: execution,
                // If key exists, merge the executions
                updateValueFactory: (key, existingExecution) =>
                {
                    // Only merge if execution isn't already in a terminal state
                    if (!IsExecutionTerminal(existingExecution.Status))
                    {
                        // Update status if necessary
                        if (IsExecutionTerminal(execution.Status))
                        {
                            existingExecution.Status = execution.Status;
                            if (execution.CompletedAt.HasValue)
                                existingExecution.CompletedAt = execution.CompletedAt;
                        }
                        
                        // Always merge variables
                        foreach (var kvp in execution.LocalVariables)
                        {
                            existingExecution.LocalVariables[kvp.Key] = kvp.Value;
                        }
                        
                        // Merge events to ensure complete history
                        foreach (var evt in execution.Events)
                        {
                            if (!existingExecution.Events.Any(e => e.EventId == evt.EventId))
                            {
                                existingExecution.Events.Add(evt);
                            }
                        }
                    }
                    return existingExecution;
                });
            
            Touch();
        }
        
        /// <summary>
        /// Merges multiple executions at once
        /// </summary>
        public void MergeExecutions(IEnumerable<ElementExecution> executions)
        {
            if (executions == null) return;
            
            foreach (var execution in executions)
            {
                MergeExecution(execution);
            }
        }
        
        /// <summary>
        /// Checks if an execution status is considered terminal (completed, failed, or terminated)
        /// </summary>
        private bool IsExecutionTerminal(ExecutionStatus status)
        {
            return status == ExecutionStatus.Completed || 
                   status == ExecutionStatus.Failed || 
                   status == ExecutionStatus.Terminated;
        }
    }
}
