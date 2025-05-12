using System;
using System.Collections.Generic;
using System.Linq;

namespace Novin.Bpmn.EventSourcing.Core.Models
{
    /// <summary>
    /// DDD‐style enumeration of BPMN process statuses.
    /// Provides static instances, lookup, and common state‐based behaviors.
    /// </summary>
     /// <summary>
    /// DDD‐style enumeration of BPMN process instance statuses.
    /// </summary>
    public sealed class ProcessInstanceStatus : IEquatable<ProcessInstanceStatus>
    {
        public static readonly ProcessInstanceStatus NotStarted = new("NotStarted");
        public static readonly ProcessInstanceStatus Starting   = new("Starting");
        public static readonly ProcessInstanceStatus Active     = new("Active");
        public static readonly ProcessInstanceStatus Waiting    = new("Waiting");
        public static readonly ProcessInstanceStatus Suspended  = new("Suspended");
        public static readonly ProcessInstanceStatus Completed  = new("Completed");
        public static readonly ProcessInstanceStatus Cancelled  = new("Cancelled");
        public static readonly ProcessInstanceStatus Terminated = new("Terminated");
        public static readonly ProcessInstanceStatus Failed     = new("Failed");

        private static readonly Dictionary<string, ProcessInstanceStatus> _instances
            = new(StringComparer.OrdinalIgnoreCase)
        {
            { NotStarted.Name,   NotStarted },
            { Starting.Name,     Starting   },
            { Active.Name,       Active     },
            { Waiting.Name,      Waiting    },
            { Suspended.Name,    Suspended  },
            { Completed.Name,    Completed  },
            { Cancelled.Name,    Cancelled  },
            { Terminated.Name,   Terminated },
            { Failed.Name,       Failed     }
        };

        public string Name { get; }

        private ProcessInstanceStatus(string name) => Name = name;

        public override string ToString() => Name;

        public bool Equals(ProcessInstanceStatus? other) =>
            other is not null && Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) =>
            obj is ProcessInstanceStatus ps && Equals(ps);

        public override int GetHashCode() =>
            Name.GetHashCode(StringComparison.OrdinalIgnoreCase);

        public static bool operator ==(ProcessInstanceStatus? a, ProcessInstanceStatus? b) =>
            ReferenceEquals(a, b) || (a is not null && a.Equals(b));

        public static bool operator !=(ProcessInstanceStatus? a, ProcessInstanceStatus? b) =>
            !(a == b);

        public static IEnumerable<ProcessInstanceStatus> List() =>
            _instances.Values;

        public static ProcessInstanceStatus FromString(string name) =>
            string.IsNullOrWhiteSpace(name) || !_instances.TryGetValue(name, out var status)
                ? NotStarted
                : status;

        public static bool TryParse(string name, out ProcessInstanceStatus status)
        {
            if (!string.IsNullOrWhiteSpace(name) && _instances.TryGetValue(name, out var found))
            {
                status = found;
                return true;
            }
            status = NotStarted;
            return false;
        }

        public bool IsTerminal =>
            this == Completed || this == Cancelled || this == Terminated || this == Failed;

        public bool IsRunning =>
            this == Active || this == Starting;

        public bool CanSuspend =>
            this == Active || this == Waiting;

        public bool CanResume =>
            this == Suspended || this == Waiting;

        public bool CanCancel =>
            !IsTerminal && this != NotStarted;

        public bool CanComplete =>
            IsRunning || this == Waiting;
    }

    /// <summary>
    /// A pending event subscription (timer, message, etc.).
    /// </summary>
    public class EventSubscription
    {
        public string SubscriptionId    { get; set; } = Guid.NewGuid().ToString();
        public string ProcessInstanceId { get; set; } = null!;
        public string ElementId         { get; set; } = null!;
        public string EventType         { get; set; } = null!;
        public string? CorrelationKey   { get; set; }
        public DateTime? DueDate        { get; set; }
    }

    /// <summary>
    /// Represents a scheduled or retryable job (e.g. timer, async task).
    /// </summary>
    public class Job
    {
        public string JobId             { get; set; } = Guid.NewGuid().ToString();
        public string ProcessInstanceId { get; set; } = null!;
        public string ExecutionId       { get; set; } = null!;
        public string Type              { get; set; } = null!;
        public Dictionary<string, object>? Payload { get; set; }
        public int RetryCount           { get; set; } = 0;
        public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
        public DateTime? DueAt          { get; set; }
    }

    /// <summary>
    /// Tracks an error/incident raised during execution.
    /// </summary>
    public class Incident
    {
        public string IncidentId        { get; set; } = Guid.NewGuid().ToString();
        public string ProcessInstanceId { get; set; } = null!;
        public string ExecutionId       { get; set; } = null!;
        public string ErrorMessage      { get; set; } = null!;
        public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
        public bool Resolved            { get; set; } = false;
        public DateTime? ResolvedAt     { get; set; }
    }

}
