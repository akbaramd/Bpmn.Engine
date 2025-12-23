using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class ProcessVariableUpdatedEvent : BaseDomainEvent
{
    public Guid ProcessId { get; }
    public string VariableKey { get; }
    public object VariableValue { get; }
    public DateTime UpdatedAt { get; }

    public ProcessVariableUpdatedEvent(Guid processId, string variableKey, object variableValue, DateTime updatedAt)
    {
        ProcessId = processId;
        VariableKey = variableKey;
        VariableValue = variableValue;
        UpdatedAt = updatedAt;
    }
}

