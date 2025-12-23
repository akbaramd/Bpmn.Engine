namespace Novin.Bpmn.Engine.Domain.Common;

/// <summary>
/// Base class for all domain events
/// </summary>
public abstract class BaseDomainEvent : IDomainEvent
{
    public Guid EventId { get; }
    public DateTime OccurredOn { get; }

    protected BaseDomainEvent()
    {
        EventId = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
    }
}

