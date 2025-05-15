using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.EventStore;

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

        var notCompletedStatuses = new[] { EventStatus.Pending, EventStatus.Failed };

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new DateTimeOffsetJsonConverter()
            }
        };

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
                    IBpmnEvent @event;

                    try
                    {
                        var assembly = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => a.GetName().Name == eventEntity.AssemblyName);

                        if (assembly == null)
                        {
                            throw new InvalidOperationException($"Assembly '{eventEntity.AssemblyName}' not loaded.");
                        }

                        var eventType = assembly.GetType(eventEntity.TypeFullName);

                        if (eventType == null)
                        {
                            throw new InvalidOperationException($"Type '{eventEntity.TypeFullName}' not found in assembly '{eventEntity.AssemblyName}'.");
                        }

                        @event = (IBpmnEvent)JsonSerializer.Deserialize(eventEntity.Payload, eventType, options)!;
                    }
                    catch (Exception dex)
                    {
                        _logger.LogError(dex, "Failed to deserialize event {EventId}. Marking as Failed.", eventEntity.EventId);
                        _eventStore.UpdateStatus(eventEntity.EventId, EventStatus.Failed, dex.Message);
                        continue;
                    }

                    try
                    {
                        _logger.LogInformation("Publishing event {EventType} - {EventId}", @event.EventType, @event.EventId);
                        await _eventBus.PublishAsync(@event, stoppingToken);

                        _eventStore.UpdateStatus(eventEntity.EventId, EventStatus.Sent);
                    }
                    catch (Exception pex)
                    {
                        _logger.LogError(pex, "Error publishing event {EventId}. Marking as Failed.", eventEntity.EventId);
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
}
