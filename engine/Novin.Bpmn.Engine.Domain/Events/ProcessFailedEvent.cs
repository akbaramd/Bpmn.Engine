using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class ProcessFailedEvent : BaseDomainEvent
{
    public Guid ProcessId { get; }
    public DateTime FailedAt { get; }
    public string ErrorMessage { get; }

    public ProcessFailedEvent(Guid processId, DateTime failedAt, string errorMessage)
    {
        ProcessId = processId;
        FailedAt = failedAt;
        ErrorMessage = errorMessage;
    }
}

