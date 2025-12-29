using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class ProcessSuspendedEvent : BaseDomainEvent
{
    public Guid ProcessId { get; }
    public DateTime SuspendedAt { get; }
    public string? Reason { get; }

    public ProcessSuspendedEvent(Guid processId, DateTime suspendedAt, string? reason = null)
    {
        ProcessId = processId;
        SuspendedAt = suspendedAt;
        Reason = reason;
    }
}

