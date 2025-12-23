using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class NodeCompletedEvent : BaseDomainEvent
{
    public Guid NodeId { get; }
    public Guid ProcessId { get; }
    public Guid TokenId { get; }
    public DateTime CompletedAt { get; }
    public Dictionary<string, object>? OutputVariables { get; }

    public NodeCompletedEvent(Guid nodeId, Guid processId, Guid tokenId, DateTime completedAt, Dictionary<string, object>? outputVariables = null)
    {
        NodeId = nodeId;
        ProcessId = processId;
        TokenId = tokenId;
        CompletedAt = completedAt;
        OutputVariables = outputVariables;
    }
}

