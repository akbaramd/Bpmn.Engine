using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public sealed class TokenLocalVariablesClearedEvent : BaseDomainEvent
{
    public Guid TokenId { get; }
    public Guid ProcessId { get; }
    public int ClearedCount { get; }
    public DateTime OccurredAtUtc { get; }

    public TokenLocalVariablesClearedEvent(
        Guid tokenId,
        Guid processId,
        int clearedCount,
        DateTime occurredAtUtc)
    {
        TokenId = tokenId;
        ProcessId = processId;
        ClearedCount = clearedCount;
        OccurredAtUtc = occurredAtUtc;
    }
}

