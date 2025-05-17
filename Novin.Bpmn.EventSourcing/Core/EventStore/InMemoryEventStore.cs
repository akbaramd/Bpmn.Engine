using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.EventStore;
using Novin.Bpmn.EventSourcing.Events;

public class InMemoryEventStore : IEventStore
{
    private readonly ConcurrentDictionary<Guid, EventEntity> _events = new();

    public void Append(EventEntity eventEntity)
    {
        if (eventEntity == null)
            throw new ArgumentNullException(nameof(eventEntity));

        eventEntity.Status = EventStatus.Pending;
        eventEntity.Timestamp = DateTime.UtcNow;

        if (!_events.TryAdd(eventEntity.EventId, eventEntity))
            throw new InvalidOperationException($"Event with ID {eventEntity.EventId} already exists");
    }

    // Append مستقیم BpmnEvent با سریالایز توسط Newtonsoft.Json
    public void Append(BpmnEvent @event)
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));

        var entity = ConvertToEventEntity(@event);

        Append(entity);
    }

    private EventEntity ConvertToEventEntity(BpmnEvent @event)
    {
        var type = @event.GetType();

        var settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            Formatting = Formatting.None
        };

        return new EventEntity
        {
            EventId = @event.EventId,
            InstanceId = @event.InstanceId,
            EventType = @event.EventType,
            Timestamp = @event.Timestamp,
            Payload = JsonConvert.SerializeObject(@event, type, settings),
            TypeFullName = type.FullName ?? string.Empty,
            AssemblyName = type.Assembly.GetName().Name ?? string.Empty,
            Status = EventStatus.Pending,
            RetryCount = 0,
            ErrorMessage = null
        };
    }

    public IReadOnlyList<EventEntity> GetEvents(Guid instanceId, EventStatus[]? statuses = null)
    {
        var query = _events.Values.Where(e => e.InstanceId == instanceId);
        if (statuses != null && statuses.Length > 0)
            query = query.Where(e => statuses.Contains(e.Status));

        return query.OrderBy(e => e.Timestamp).ToList();
    }

    public void UpdateStatus(Guid eventId, EventStatus newStatus, string? errorMessage = null,int? retryCount = null)
    {
        if (!_events.TryGetValue(eventId, out var eventEntity))
            throw new KeyNotFoundException($"Event with ID {eventId} not found");

        eventEntity.RetryCount = retryCount ?? 0;
        eventEntity.Status = newStatus;
        eventEntity.ErrorMessage = errorMessage;
    }

    public IReadOnlyList<EventEntity> GetAll(EventStatus[]? statuses = null)
    {
        var query = _events.Values.AsEnumerable();
        if (statuses != null && statuses.Length > 0)
            query = query.Where(e => statuses.Contains(e.Status));

        return query.OrderBy(e => e.Timestamp).ToList();
    }

    public IReadOnlyList<EventEntity> GetIncompletedEvents(int size)
    {
        var incompleteStatuses = new[] { EventStatus.Pending, EventStatus.Failed };
        return _events.Values
            .Where(e => incompleteStatuses.Contains(e.Status))
            .OrderBy(e => e.Timestamp)
            .Take(size)
            .ToList();
    }


    

}



