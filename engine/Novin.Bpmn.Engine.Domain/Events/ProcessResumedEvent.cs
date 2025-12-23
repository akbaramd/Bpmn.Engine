using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class ProcessResumedEvent : BaseDomainEvent
{
    public Guid ProcessId { get; }
    public DateTime ResumedAt { get; }

    public ProcessResumedEvent(Guid processId, DateTime resumedAt)
    {
        ProcessId = processId;
        ResumedAt = resumedAt;
    }
}

