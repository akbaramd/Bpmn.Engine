using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Domain.Events;

public class NodeCreatedEvent : BaseDomainEvent
{
    public Guid NodeId { get; }
    public Guid ProcessId { get; }
    public string NodeName { get; }
    public string ElementId { get; }
    public NodeType NodeType { get; }
    public DateTime CreatedAt { get; }

    public NodeCreatedEvent(Guid nodeId, Guid processId, string nodeName, string elementId, NodeType nodeType, DateTime createdAt)
    {
        NodeId = nodeId;
        ProcessId = processId;
        NodeName = nodeName;
        ElementId = elementId;
        NodeType = nodeType;
        CreatedAt = createdAt;
    }
}

