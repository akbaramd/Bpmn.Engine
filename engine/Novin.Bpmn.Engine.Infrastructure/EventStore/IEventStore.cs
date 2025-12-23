using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Infrastructure.EventStore;

/// <summary>
/// Event store interface for event sourcing
/// </summary>
public interface IEventStore
{
    Task SaveEventAsync<TEvent>(TEvent @event, Guid aggregateId, int aggregateVersion, CancellationToken cancellationToken = default) where TEvent : IDomainEvent;
    Task<IEnumerable<IDomainEvent>> GetEventsAsync(Guid aggregateId, CancellationToken cancellationToken = default);
    Task<IEnumerable<IDomainEvent>> GetAllEventsAsync(CancellationToken cancellationToken = default);
}

