using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public sealed class TokenLocalVariableSetEvent : BaseDomainEvent
{
    public Guid TokenId { get; }
    public Guid ProcessId { get; }
    public string Name { get; }
    public DateTime OccurredAtUtc { get; }

    public TokenLocalVariableSetEvent(
        Guid tokenId,
        Guid processId,
        string name,
        DateTime occurredAtUtc)
    {
        TokenId = tokenId;
        ProcessId = processId;
        Name = name;
        OccurredAtUtc = occurredAtUtc;
    }
}

