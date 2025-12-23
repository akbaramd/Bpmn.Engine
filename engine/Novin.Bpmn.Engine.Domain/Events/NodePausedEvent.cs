using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class NodePausedEvent : BaseDomainEvent
{
    public Guid NodeId { get; }
    public Guid ProcessId { get; }
    public Guid TokenId { get; }
    public DateTime PausedAt { get; }
    public string? Reason { get; }

    public NodePausedEvent(Guid nodeId, Guid processId, Guid tokenId, DateTime pausedAt, string? reason = null)
    {
        NodeId = nodeId;
        ProcessId = processId;
        TokenId = tokenId;
        PausedAt = pausedAt;
        Reason = reason;
    }
}

