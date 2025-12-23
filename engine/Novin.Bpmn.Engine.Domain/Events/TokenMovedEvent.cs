using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class TokenMovedEvent : BaseDomainEvent
{
    public Guid TokenId { get; }
    public Guid ProcessId { get; }
    public string FromElementId { get; }
    public string ToElementId { get; }
    public DateTime MovedAt { get; }

    public TokenMovedEvent(Guid tokenId, Guid processId, string fromElementId, string toElementId, DateTime movedAt)
    {
        TokenId = tokenId;
        ProcessId = processId;
        FromElementId = fromElementId;
        ToElementId = toElementId;
        MovedAt = movedAt;
    }
}

