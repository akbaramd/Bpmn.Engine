using Novin.Bpmn.EventSourcing.Contracts;
using System;

namespace Novin.Bpmn.EventSourcing.Events
{
    // ===========================
    // Process-Level Events
    // ===========================

    /// <summary>
    /// Fired when a new process instance is created (started).
    /// </summary>
    public record ProcessStarted : BpmnEvent
    {
        public override string EventType => nameof(ProcessStarted);

        /// <summary>
        /// The version of the process definition.
        /// </summary>
        public int DefinitionVersion { get; init; } = 1;

        public Dictionary<string, object?> InitializeVariables { get; set; } = [];

        /// <summary>
        /// شناسه StartEvent که trigger شده است.
        /// اگر null باشد، None StartEvent (یا اولین StartEvent) استفاده می‌شود.
        /// </summary>
        public string? StartEventId { get; init; }

    }

    /// <summary>
    /// Fired when a suspended process instance is resumed.
    /// </summary>
    public record ProcessResumed : BpmnEvent
    {
        public override string EventType => nameof(ProcessResumed);

        /// <summary>
        /// Optional reason or comment for the resume action.
        /// </summary>
        public string? ResumeReason { get; init; }
    }


    // cancle
    public record ProcessCancelled : BpmnEvent
    {
        public override string EventType => nameof(ProcessCancelled);

        /// <summary>
        /// Reason for cancellation (e.g., user-cancellation, system-shutdown). 
        /// </summary>
        public required string Reason { get; init; }
    }       

    /// <summary>
    /// Fired when a process instance completes normally.
    /// </summary>
    public record ProcessCompleted : BpmnEvent
    {
        public override string EventType => nameof(ProcessCompleted);

        /// <summary>
        /// Timestamp when the process reached its natural end.
        /// </summary>
        public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Fired when a process instance is explicitly terminated.
    /// </summary>
    public record ProcessTerminated : BpmnEvent
    {
        public override string EventType => nameof(ProcessTerminated);

        /// <summary>
        /// Reason for termination (e.g., user-cancellation, system-shutdown).
        /// </summary>
        public required string TerminationReason { get; init; }
    }

    public record ProcessFailureEvent : BpmnEvent
    {
        public string FailureReason { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int Version { get; set; } = 1;
        public override string EventType => nameof(ProcessFailureEvent);
    }
    
public record ProcessSuspended : BpmnEvent
    {
        public override string EventType => nameof(ProcessSuspended);

        /// <summary>
        /// Reason for termination (e.g., user-cancellation, system-shutdown).
        /// </summary>
        public required string SuspendReason { get; init; }
    }


    /// <summary>
    /// Fired when a process instance fails due to an error.
    /// </summary>
    public record ProcessFailed : BpmnEvent
    {
        public override string EventType => nameof(ProcessFailed);

        /// <summary>
        /// Error code or exception message that caused the failure.
        /// </summary>
        public required string ErrorMessage { get; init; }

        /// <summary>
        /// (Optional) Stack trace or diagnostic details.
        /// </summary>
        public string? ErrorDetails { get; init; }
    }

    public record ProcessRestarted : BpmnEvent
    {
        public override string EventType => nameof(ProcessRestarted);
    }   
}
