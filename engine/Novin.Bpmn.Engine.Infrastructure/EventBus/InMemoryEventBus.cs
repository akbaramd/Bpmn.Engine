using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Common;
using System.Collections.Concurrent;

namespace Novin.Bpmn.Engine.Infrastructure.EventBus;

/// <summary>
/// In-memory implementation of event bus
/// </summary>
public class InMemoryEventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<object>> _handlers = new();
    private readonly ILogger<InMemoryEventBus> _logger;

    public InMemoryEventBus(ILogger<InMemoryEventBus> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IDomainEvent
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));

        _logger.LogInformation("Publishing event: {EventType} with ID: {EventId}", typeof(TEvent).Name, @event.EventId);

        var eventType = typeof(TEvent);
        
        if (!_handlers.TryGetValue(eventType, out var handlers) || handlers.Count == 0)
        {
            _logger.LogWarning("No handlers registered for event type: {EventType}", eventType.Name);
            return;
        }

        var tasks = handlers
            .OfType<IEventHandler<TEvent>>()
            .Select(handler => handler.HandleAsync(@event, cancellationToken));

        await Task.WhenAll(tasks);

        _logger.LogInformation("Event published successfully: {EventType} with ID: {EventId}", typeof(TEvent).Name, @event.EventId);
    }

    public void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IDomainEvent
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(TEvent);
        var handlers = _handlers.GetOrAdd(eventType, _ => new List<object>());
        
        lock (handlers)
        {
            handlers.Add(handler);
        }

        _logger.LogInformation("Handler registered for event type: {EventType}", eventType.Name);
    }
}

