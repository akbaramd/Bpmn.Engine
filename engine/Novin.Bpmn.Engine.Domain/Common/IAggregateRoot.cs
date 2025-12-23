namespace Novin.Bpmn.Engine.Domain.Common;

/// <summary>
/// Marker interface for aggregate roots in DDD
/// </summary>
public interface IAggregateRoot
{
    Guid Id { get; }
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

