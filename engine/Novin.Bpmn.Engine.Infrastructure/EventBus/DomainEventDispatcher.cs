using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Infrastructure.EventStore;

namespace Novin.Bpmn.Engine.Infrastructure.EventBus;

/// <summary>
/// Dispatches domain events from aggregates to MediatR and event store
/// </summary>
public class DomainEventDispatcher
{
    private readonly IMediator _mediator;
    private readonly IEventStore _eventStore;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(
        IMediator mediator,
        IEventStore eventStore,
        ILogger<DomainEventDispatcher> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task DispatchEventsAsync(List<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        if (domainEvents == null)
            throw new ArgumentNullException(nameof(domainEvents));

        if (domainEvents.Count == 0)
            return;

        _logger.LogInformation("Dispatching {Count} domain events for event {EventId}", 
            domainEvents.Count, domainEvents[0].EventId);

        foreach (var domainEvent in domainEvents)
        {
            // Save to event store (version will be calculated from event count)
            await _eventStore.SaveEventAsync(domainEvent, domainEvent.EventId, (int)domainEvent.OccurredOn.Ticks, cancellationToken);
            
            // Publish to MediatR (which will trigger INotificationHandler implementations)
            await _mediator.Publish(domainEvent, cancellationToken);
        }
    }
}
