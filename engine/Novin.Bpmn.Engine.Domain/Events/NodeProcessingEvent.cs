using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class NodeProcessingEvent : BaseDomainEvent
{
    public Guid NodeId { get; }
    public Guid ProcessId { get; }
    public Guid TokenId { get; }
    public DateTime StartedAt { get; }

    public NodeProcessingEvent(Guid nodeId, Guid processId, Guid tokenId, DateTime startedAt)
    {
        NodeId = nodeId;
        ProcessId = processId;
        TokenId = tokenId;
        StartedAt = startedAt;
    }
}

