using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Infrastructure.EventBus;

/// <summary>
/// Dispatches domain events from aggregates to MediatR and event store
/// </summary>
public class DomainEventDispatcher
{
    private readonly IMediator _mediator;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(
        IMediator mediator,
        ILogger<DomainEventDispatcher> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task DispatchEventsAsync(List<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        if (domainEvents == null)
            throw new ArgumentNullException(nameof(domainEvents));

        if (domainEvents.Count == 0)
            return;



        _logger.LogDebug(
            "[EVENT-DISPATCH] Dispatching {Count} domain events. EventTypes={Types}",
            domainEvents.Count,
            string.Join(", ", domainEvents.Select(e => e.GetType().Name)));

        foreach (var domainEvent in domainEvents)
        {
            _logger.LogTrace(
                "[EVENT-DISPATCH] Publishing event. EventType={Type} EventId={EventId}",
                domainEvent.GetType().Name,
                domainEvent.GetType().GetProperty("ProcessId")?.GetValue(domainEvent) ?? "N/A");
            
            // Publish to MediatR (which will trigger INotificationHandler implementations)
            await _mediator.Publish(domainEvent, cancellationToken);
        }
        
        _logger.LogDebug("[EVENT-DISPATCH] All events dispatched successfully.");
    }
}
