using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Events;

namespace Novin.Bpmn.EventSourcing.Core.Models
{
    /// <summary>
    /// Represents one firing (execution) of a BPMN element.
    /// Tracks its lifecycle, associated events, timestamps, status, and executability.
    /// </summary>
    public class ElementExecution
    {
        // ───────────────────────────────────────────────────────────────────────
        // Identity & References
        // ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Unique identifier for this execution instance
        /// </summary>
        public string ExecutionId { get; set; }

        /// <summary>
        /// The process instance this execution belongs to
        /// </summary>
        public string ProcessInstanceId { get; set; }

        /// <summary>
        /// The BPMN element being executed
        /// </summary>
        public string ElementId { get; set; }

        /// <summary>
        /// The type of the BPMN element as a string for serialization
        /// </summary>
        public string ElementTypeName { get; set; }

        /// <summary>
        /// The type of the BPMN element (task, gateway, event, etc.)
        /// </summary>
        [JsonIgnore]
        public BpmnElementType ElementType 
        { 
            get => BpmnElementType.FromName(ElementTypeName); 
            set => ElementTypeName = value?.Name ?? "Unknown";
        }

        // ───────────────────────────────────────────────────────────────────────
        // Lifecycle Timestamps
        // ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// When execution started
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// When execution completed (success, failure, or termination)
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        // ───────────────────────────────────────────────────────────────────────
        // Status & Control
        // ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Current status of this execution
        /// </summary>
        public ExecutionStatus Status { get; set; }

        /// <summary>
        /// If failed, the reason for failure
        /// </summary>
        public string? FailureReason { get; set; }

        /// <summary>
        /// Indicates whether this execution should run its business logic.
        /// If false, this execution only routes tokens and emits routing events.
        /// </summary>
        public bool IsExecutable { get; set; }

        // ───────────────────────────────────────────────────────────────────────
        // Event Tracking
        // ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// IDs of BPMN events attached to this execution
        /// </summary>
        public List<string> EventIds { get; set; } = new();

        /// <summary>
        /// Chronological list of serializable event objects
        /// </summary>
        public List<SerializableBpmnEvent> Events { get; set; } = new();

        /// <summary>
        /// Counts of each event type seen in this execution
        /// </summary>
        public Dictionary<string, int> EventTypeCounts { get; set; } = new();

        // ───────────────────────────────────────────────────────────────────────
        // Extension Data
        // ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Local variables scoped to this execution
        /// </summary>
        public Dictionary<string, object> LocalVariables { get; set; } = new();

        /// <summary>
        /// Arbitrary additional properties
        /// </summary>
        public Dictionary<string, string> Properties { get; set; } = new();

        // ───────────────────────────────────────────────────────────────────────
        // Construction & Factory
        // ───────────────────────────────────────────────────────────────────────

        public ElementExecution() { }

        /// <summary>
        /// Create and start a new element execution.
        /// </summary>
        /// <param name="processInstanceId">ID of the process instance</param>
        /// <param name="elementId">ID of the BPMN element</param>
        /// <param name="elementType">Type of the BPMN element</param>
        /// <param name="localVariables">Optional local variables</param>
        /// <param name="isExecutable">Whether this execution runs business logic</param>
        public static ElementExecution StartNew(
            string processInstanceId,
            string elementId,
            BpmnElementType elementType,
            Dictionary<string, object> localVariables = null,
            bool isExecutable = true)
        {
            if (string.IsNullOrWhiteSpace(processInstanceId))
                throw new ArgumentException("processInstanceId is required", nameof(processInstanceId));
            if (string.IsNullOrWhiteSpace(elementId))
                throw new ArgumentException("elementId is required", nameof(elementId));

            return new ElementExecution
            {
                ExecutionId = Guid.NewGuid().ToString(),
                ProcessInstanceId = processInstanceId,
                ElementId = elementId,
                ElementType = elementType,
                StartedAt = DateTime.UtcNow,
                Status = ExecutionStatus.Active,
                IsExecutable = isExecutable,
                LocalVariables = localVariables ?? new Dictionary<string, object>()
            };
        }

        // ───────────────────────────────────────────────────────────────────────
        // Domain Behaviors
        // ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Attach a BPMN event to this execution.
        /// If not executable, only routing-related events are allowed.
        /// </summary>
        public void AddEvent(IBpmnEvent bpmnEvent)
        {
            if (bpmnEvent is null)
                throw new ArgumentNullException(nameof(bpmnEvent));

            if (Status != ExecutionStatus.Active && Status != ExecutionStatus.Waiting)
                throw new InvalidOperationException($"Cannot add events when status is {Status}.");

            // if this path is non-executable, you may choose to ignore business events
            if (!IsExecutable && !IsRoutingEvent(bpmnEvent.EventType))
                return;

            var id = bpmnEvent.EventId.ToString();
            EventIds.Add(id);

            if (EventTypeCounts.ContainsKey(bpmnEvent.EventType))
                EventTypeCounts[bpmnEvent.EventType]++;
            else
                EventTypeCounts[bpmnEvent.EventType] = 1;

            // Convert to serializable event
            var serializableEvent = SerializableBpmnEvent.FromEvent(bpmnEvent);
            Events.Add(serializableEvent);
        }

        /// <summary>
        /// Determines if an event type is allowed on non-executable executions.
        /// Customize as needed for routing/fork/merge events.
        /// </summary>
        private bool IsRoutingEvent(string eventType)
        {
            return eventType.Contains("Gateway")    // e.g. ParallelGatewayCompleted
                || eventType.Contains("SequenceFlow")
                || eventType.Contains("TokenArrived");
        }

        /// <summary>
        /// Mark this execution as completed successfully.
        /// </summary>
        public void Complete()
        {
            EnsureActive();
            Status = ExecutionStatus.Completed;
            CompletedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Mark this execution as failed, optionally with a reason.
        /// </summary>
        public void Fail(string reason)
        {
            EnsureActive();
            Status = ExecutionStatus.Failed;
            FailureReason = reason;
            CompletedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Terminate this execution immediately.
        /// </summary>
        public void Terminate()
        {
            if (Status == ExecutionStatus.Completed
             || Status == ExecutionStatus.Failed
             || Status == ExecutionStatus.Terminated)
            {
                throw new InvalidOperationException($"Cannot terminate when status is {Status}.");
            }
            Status = ExecutionStatus.Terminated;
            CompletedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Suspend a running execution (e.g., for timers).
        /// </summary>
        public void Suspend()
        {
            EnsureActive();
            Status = ExecutionStatus.Suspended;
        }

        /// <summary>
        /// Resume a suspended execution.
        /// </summary>
        public void Resume()
        {
            if (Status != ExecutionStatus.Suspended && Status != ExecutionStatus.Waiting)
                throw new InvalidOperationException($"Cannot resume when status is {Status}.");

            Status = ExecutionStatus.Active;
        }

        /// <summary>
        /// Set a local variable value
        /// </summary>
        public void SetVariable(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Variable name required", nameof(name));

            LocalVariables[name] = value;
        }

        //SetVariables
        /// <summary>
        /// Set multiple local variable values at once
        /// </summary>
        public void SetVariables(IDictionary<string, object> variables)
        {
            if (variables == null)
                throw new ArgumentNullException(nameof(variables));

            foreach (var (name, value) in variables)
            {
                SetVariable(name, value);
            }
        }

        /// <summary>
        /// Get a local variable value
        /// </summary>
        public bool TryGetVariable<T>(string name, out T value)
        {
            if (LocalVariables.TryGetValue(name, out var obj) && obj is T cast)
            {
                value = cast;
                return true;
            }
            value = default!;
            return false;
        }

        private void EnsureActive()
        {
            if (Status != ExecutionStatus.Active && Status != ExecutionStatus.Waiting)
                throw new InvalidOperationException(
                    $"Operation allowed only in Active/Waiting states, current state: {Status}");
        }
    }

    /// <summary>
    /// Possible statuses of an ElementExecution.
    /// </summary>
    public enum ExecutionStatus
    {
        Active,
        Completed,
        Terminated,
        Failed,
        Waiting,
        Suspended
    }
}
