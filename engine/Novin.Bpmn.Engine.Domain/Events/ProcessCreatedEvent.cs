using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class ProcessCreatedEvent : BaseDomainEvent
{
    public Guid ProcessId { get; }
    public string ProcessName { get; }
    public string ProcessDefinitionId { get; }
    public DateTime CreatedAt { get; }

    public ProcessCreatedEvent(Guid processId, string processName, string processDefinitionId, DateTime createdAt)
    {
        ProcessId = processId;
        ProcessName = processName;
        ProcessDefinitionId = processDefinitionId;
        CreatedAt = createdAt;
    }
}

