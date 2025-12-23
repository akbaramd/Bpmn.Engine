using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Infrastructure.EventStore;

/// <summary>
/// In-memory implementation of event store
/// </summary>
public class InMemoryEventStore : IEventStore
{
    private readonly ConcurrentDictionary<Guid, List<StoredEvent>> _events = new();
    private readonly ILogger<InMemoryEventStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public InMemoryEventStore(ILogger<InMemoryEventStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public Task SaveEventAsync<TEvent>(TEvent @event, Guid aggregateId, int aggregateVersion, CancellationToken cancellationToken = default) where TEvent : IDomainEvent
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));

        var storedEvent = new StoredEvent
        {
            EventId = @event.EventId,
            AggregateId = aggregateId,
            EventType = typeof(TEvent).AssemblyQualifiedName ?? typeof(TEvent).Name,
            EventData = JsonSerializer.Serialize(@event, _jsonOptions),
            AggregateVersion = aggregateVersion,
            OccurredOn = @event.OccurredOn
        };

        var events = _events.GetOrAdd(aggregateId, _ => new List<StoredEvent>());
        
        lock (events)
        {
            events.Add(storedEvent);
        }

        _logger.LogInformation("Event stored: {EventType} for aggregate {AggregateId} at version {Version}", 
            typeof(TEvent).Name, aggregateId, aggregateVersion);

        return Task.CompletedTask;
    }

    public Task<IEnumerable<IDomainEvent>> GetEventsAsync(Guid aggregateId, CancellationToken cancellationToken = default)
    {
        if (!_events.TryGetValue(aggregateId, out var storedEvents))
        {
            _logger.LogWarning("No events found for aggregate: {AggregateId}", aggregateId);
            return Task.FromResult(Enumerable.Empty<IDomainEvent>());
        }

        var events = storedEvents
            .OrderBy(e => e.AggregateVersion)
            .Select(DeserializeEvent)
            .Where(e => e != null)
            .Cast<IDomainEvent>();

        return Task.FromResult(events);
    }

    public Task<IEnumerable<IDomainEvent>> GetAllEventsAsync(CancellationToken cancellationToken = default)
    {
        var allEvents = _events.Values
            .SelectMany(events => events)
            .OrderBy(e => e.OccurredOn)
            .Select(DeserializeEvent)
            .Where(e => e != null)
            .Cast<IDomainEvent>();

        return Task.FromResult(allEvents);
    }

    private IDomainEvent? DeserializeEvent(StoredEvent storedEvent)
    {
        try
        {
            var eventType = Type.GetType(storedEvent.EventType);
            if (eventType == null)
            {
                _logger.LogWarning("Event type not found: {EventType}", storedEvent.EventType);
                return null;
            }

            var @event = JsonSerializer.Deserialize(storedEvent.EventData, eventType, _jsonOptions);
            return @event as IDomainEvent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing event: {EventId}", storedEvent.EventId);
            return null;
        }
    }
}

