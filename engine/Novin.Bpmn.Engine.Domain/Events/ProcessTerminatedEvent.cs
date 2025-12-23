using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class ProcessTerminatedEvent : BaseDomainEvent
{
    public Guid ProcessId { get; }
    public DateTime TerminatedAt { get; }
    public string? Reason { get; }

    public ProcessTerminatedEvent(Guid processId, DateTime terminatedAt, string? reason = null)
    {
        ProcessId = processId;
        TerminatedAt = terminatedAt;
        Reason = reason;
    }
}

