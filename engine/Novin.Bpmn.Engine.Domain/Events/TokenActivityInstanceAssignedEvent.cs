using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public sealed class TokenActivityInstanceAssignedEvent : BaseDomainEvent
{
    public Guid TokenId { get; }
    public Guid ProcessId { get; }
    public Guid ActivityInstanceId { get; }
    public DateTime OccurredAtUtc { get; }

    public TokenActivityInstanceAssignedEvent(
        Guid tokenId,
        Guid processId,
        Guid activityInstanceId,
        DateTime occurredAtUtc)
    {
        TokenId = tokenId;
        ProcessId = processId;
        ActivityInstanceId = activityInstanceId;
        OccurredAtUtc = occurredAtUtc;
    }
}

