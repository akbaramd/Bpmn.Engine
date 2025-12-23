using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class ProcessCompletedEvent : BaseDomainEvent
{
    public Guid ProcessId { get; }
    public DateTime CompletedAt { get; }

    public ProcessCompletedEvent(Guid processId, DateTime completedAt)
    {
        ProcessId = processId;
        CompletedAt = completedAt;
    }
}

