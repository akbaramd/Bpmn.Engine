using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.EventStore;
using Novin.Bpmn.EventSourcing.Events;

public class EventWorkerService : BackgroundService
{
    private readonly IEventStore _eventStore;
    private readonly IEventBus _eventBus;
    private readonly ILogger<EventWorkerService> _logger;

    public EventWorkerService(
        IEventStore eventStore,
        IEventBus eventBus,
        ILogger<EventWorkerService> logger)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EventWorkerService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var eventEntities = _eventStore.GetIncompletedEvents();

                if (eventEntities.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                foreach (var eventEntity in eventEntities)
                {
                    BpmnEvent? bpmnEvent = null;

                    try
                    {
                        bpmnEvent = DeserializeEvent(eventEntity);
                        if (bpmnEvent == null)
                        {
                            _logger.LogWarning("Could not deserialize event {EventId}", eventEntity.EventId);
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize event {EventId}", eventEntity.EventId);
                        _eventStore.UpdateStatus(eventEntity.EventId, EventStatus.Failed, ex.Message);
                        continue;
                    }

                    try
                    {
                        _logger.LogInformation("Publishing event {EventType} - {EventId}", bpmnEvent.EventType, bpmnEvent.EventId);
                        await _eventBus.PublishAsync(bpmnEvent, stoppingToken);
                        _eventStore.UpdateStatus(eventEntity.EventId, EventStatus.Sent);
                    }
                    catch (Exception pex)
                    {
                        _logger.LogError(pex, "Error publishing event {EventId}", eventEntity.EventId);
                        _eventStore.UpdateStatus(eventEntity.EventId, EventStatus.Failed, pex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EventWorkerService main loop.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("EventWorkerService stopped.");
    }

    public BpmnEvent? DeserializeEvent(EventEntity entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Payload) || string.IsNullOrWhiteSpace(entity.TypeFullName))
            return null;

        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == entity.AssemblyName);

        if (assembly == null)
            throw new InvalidOperationException($"Assembly '{entity.AssemblyName}' not loaded.");

        var eventType = assembly.GetType(entity.TypeFullName);

        if (eventType == null)
            throw new InvalidOperationException($"Type '{entity.TypeFullName}' not found in assembly '{entity.AssemblyName}'.");

        var settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat
        };

        var deserialized = JsonConvert.DeserializeObject(entity.Payload, eventType, settings);

        if (deserialized != null)
        {
            var props = eventType.GetProperties();
            foreach (var prop in props)
            {
                if (prop.PropertyType == typeof(Dictionary<string, object?>))
                {
                    var val = prop.GetValue(deserialized);
                    if (val is JObject jObj)
                    {
                        var dict = (Dictionary<string, object?>)JsonHelper.ConvertJTokenToObject(jObj);
                        prop.SetValue(deserialized, dict);
                    }
                }
            }
        }

        return deserialized as BpmnEvent;
    }
}

public static class JsonHelper
{
    public static object? ConvertJTokenToObject(JToken token)
    {
        return token.Type switch
        {
            JTokenType.Object => token.Children<JProperty>()
                                     .ToDictionary(prop => prop.Name, prop => ConvertJTokenToObject(prop.Value)),

            JTokenType.Array => token.Select(ConvertJTokenToObject).ToList(),

            JTokenType.Integer => token.ToObject<int>(),

            JTokenType.Float => token.ToObject<double>(),

            JTokenType.String => token.ToObject<string>(),

            JTokenType.Boolean => token.ToObject<bool>(),

            JTokenType.Null or JTokenType.Undefined => null,

            _ => token.ToString()
        };
    }
}
