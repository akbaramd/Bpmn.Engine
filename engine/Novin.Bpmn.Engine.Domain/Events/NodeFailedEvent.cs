using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class NodeFailedEvent : BaseDomainEvent
{
    public Guid NodeId { get; }
    public Guid ProcessId { get; }
    public Guid TokenId { get; }
    public DateTime FailedAt { get; }
    public string ErrorCode { get; }
    public string ErrorMessage { get; }

    public NodeFailedEvent(Guid nodeId, Guid processId, Guid tokenId, DateTime failedAt, string errorCode, string errorMessage)
    {
        NodeId = nodeId;
        ProcessId = processId;
        TokenId = tokenId;
        FailedAt = failedAt;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }
}

