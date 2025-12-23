using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class ProcessSuspendedEvent : BaseDomainEvent
{
    public Guid ProcessId { get; }
    public DateTime SuspendedAt { get; }

    public ProcessSuspendedEvent(Guid processId, DateTime suspendedAt)
    {
        ProcessId = processId;
        SuspendedAt = suspendedAt;
    }
}

