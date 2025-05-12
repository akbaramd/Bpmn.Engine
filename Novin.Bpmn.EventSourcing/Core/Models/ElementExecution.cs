using System;
using System.Collections.Generic;
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
        public string ExecutionId { get; private set; }

        /// <summary>
        /// The process instance this execution belongs to
        /// </summary>
        public string ProcessInstanceId { get; private set; }

        /// <summary>
        /// The BPMN element being executed
        /// </summary>
        public string ElementId { get; private set; }

        /// <summary>
        /// The type of the BPMN element (task, gateway, event, etc.)
        /// </summary>
        public BpmnElementType ElementType { get; private set; }


        // ───────────────────────────────────────────────────────────────────────
        // Lifecycle Timestamps
        // ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// When execution started
        /// </summary>
        public DateTime StartedAt { get; private set; }

        /// <summary>
        /// When execution completed (success, failure, or termination)
        /// </summary>
        public DateTime? CompletedAt { get; private set; }


        // ───────────────────────────────────────────────────────────────────────
        // Status & Control
        // ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Current status of this execution
        /// </summary>
        public ExecutionStatus Status { get; private set; }

        /// <summary>
        /// If failed, the reason for failure
        /// </summary>
        public string? FailureReason { get; private set; }

        /// <summary>
        /// Indicates whether this execution should run its business logic.
        /// If false, this execution only routes tokens and emits routing events.
        /// </summary>
        public bool IsExecutable { get; private set; }


        // ───────────────────────────────────────────────────────────────────────
        // Event Tracking
        // ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// IDs of BPMN events attached to this execution
        /// </summary>
        public List<string> EventIds { get; private set; } = new();

        /// <summary>
        /// Chronological list of full event objects (in-memory only)
        /// </summary>
        public List<IBpmnEvent> Events { get; } = new();

        /// <summary>
        /// Counts of each event type seen in this execution
        /// </summary>
        public Dictionary<string, int> EventTypeCounts { get; private set; } = new();


        // ───────────────────────────────────────────────────────────────────────
        // Extension Data
        // ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Local variables scoped to this execution
        /// </summary>
        public Dictionary<string, object>? LocalVariables { get; private set; }

        /// <summary>
        /// Arbitrary additional properties
        /// </summary>
        public Dictionary<string, string> Properties { get; private set; } = new();


        // ───────────────────────────────────────────────────────────────────────
        // Construction & Factory
        // ───────────────────────────────────────────────────────────────────────

        protected ElementExecution() { }

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
            Dictionary<string, object>? localVariables = null,
            bool isExecutable = true)
        {
            if (string.IsNullOrWhiteSpace(processInstanceId))
                throw new ArgumentException("processInstanceId is required", nameof(processInstanceId));
            if (string.IsNullOrWhiteSpace(elementId))
                throw new ArgumentException("elementId is required", nameof(elementId));

            return new ElementExecution
            {
                ExecutionId        = Guid.NewGuid().ToString(),
                ProcessInstanceId  = processInstanceId,
                ElementId          = elementId,
                ElementType        = elementType,
                StartedAt          = DateTime.UtcNow,
                Status             = ExecutionStatus.Active,
                IsExecutable       = isExecutable,
                LocalVariables     = localVariables
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

            Events.Add(bpmnEvent);
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
