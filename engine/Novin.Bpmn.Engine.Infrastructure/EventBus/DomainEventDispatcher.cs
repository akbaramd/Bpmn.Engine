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



        foreach (var domainEvent in domainEvents)
        {
            // Publish to MediatR (which will trigger INotificationHandler implementations)
            await _mediator.Publish(domainEvent, cancellationToken);
        }
    }
}
