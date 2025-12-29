using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public sealed class TokenActivityInstanceClearedEvent : BaseDomainEvent
{
    public Guid TokenId { get; }
    public Guid ProcessId { get; }
    public Guid? PreviousActivityInstanceId { get; }
    public DateTime OccurredAtUtc { get; }

    public TokenActivityInstanceClearedEvent(
        Guid tokenId,
        Guid processId,
        Guid? previousActivityInstanceId,
        DateTime occurredAtUtc)
    {
        TokenId = tokenId;
        ProcessId = processId;
        PreviousActivityInstanceId = previousActivityInstanceId;
        OccurredAtUtc = occurredAtUtc;
    }
}

