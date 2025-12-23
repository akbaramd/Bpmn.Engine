using System.Collections.ObjectModel;

namespace Novin.Bpmn.Engine.Domain.Common;

/// <summary>
/// Base class for aggregate roots with domain event support
/// </summary>
public abstract class BaseAggregateRoot : BaseEntity, IAggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = new();
    
    public IReadOnlyCollection<IDomainEvent> DomainEvents => new ReadOnlyCollection<IDomainEvent>(_domainEvents);

    protected BaseAggregateRoot() : base()
    {
    }

    protected BaseAggregateRoot(Guid id) : base(id)
    {
    }

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

