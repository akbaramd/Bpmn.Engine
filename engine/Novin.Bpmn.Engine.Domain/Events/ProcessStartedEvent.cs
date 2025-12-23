using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class ProcessStartedEvent : BaseDomainEvent
{
    public Guid ProcessId { get; }
    public DateTime StartedAt { get; }

    public ProcessStartedEvent(Guid processId, DateTime startedAt)
    {
        ProcessId = processId;
        StartedAt = startedAt;
    }
}

