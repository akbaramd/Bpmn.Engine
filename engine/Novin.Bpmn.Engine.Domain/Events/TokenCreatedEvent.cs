using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class TokenCreatedEvent : BaseDomainEvent
{
    public Guid TokenId { get; }
    public Guid ProcessId { get; }
    public string InitialElementId { get; }
    public Guid? ParentTokenId { get; }
    public DateTime CreatedAt { get; }

    public TokenCreatedEvent(Guid tokenId, Guid processId, string initialElementId, Guid? parentTokenId, DateTime createdAt)
    {
        TokenId = tokenId;
        ProcessId = processId;
        InitialElementId = initialElementId;
        ParentTokenId = parentTokenId;
        CreatedAt = createdAt;
    }
}

