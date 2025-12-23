using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;

namespace Novin.Bpmn.EventSourcing.Core;

public class EventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventBus> _logger;
    private readonly ConcurrentDictionary<string, IStreamProcessor> _processors = new();

    public EventBus(IServiceProvider serviceProvider, ILogger<EventBus> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync(IBpmnEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event == null) throw new ArgumentNullException(nameof(@event));

        var eventType = @event.GetType();

        _logger.LogDebug("Publishing event {EventType} for instance {InstanceId}", eventType.Name, @event.InstanceId);

        try
        {
            // دریافت و اجرای هندلرهای مربوط به نوع رویداد به صورت دینامیک
            await HandleWithRegisteredHandlers(@event, eventType, cancellationToken);

            // پردازش رویداد توسط StreamProcessor ها
            await HandleWithStreamProcessors(@event, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing event {EventType} for instance {InstanceId}", eventType.Name, @event.InstanceId);
            throw;
        }
    }

    private async Task HandleWithRegisteredHandlers(IBpmnEvent @event, Type eventType, CancellationToken cancellationToken)
    {
        // اینترفیس هندلر generic برای نوع رویداد خاص
        var handlerInterfaceType = typeof(IBpmnEventHandler<>).MakeGenericType(eventType);

        // گرفتن همه هندلرهای ثبت شده از DI برای این نوع اینترفیس
        var handlers = (IEnumerable<object>)_serviceProvider.GetServices(handlerInterfaceType).ToList();

        Console.WriteLine($"[EventBus] Looking for handlers for event type: {eventType.Name}, HandlerType: {handlerInterfaceType.FullName}");
        Console.WriteLine($"[EventBus] Found {handlers.Count()} handlers for {eventType.Name}");

        // If no handlers found for exact type, try base types (e.g., ElementProcessing for ScriptTaskProcessing)
        if (!handlers.Any())
        {
            Console.WriteLine($"[EventBus] WARNING: No handlers found for {eventType.Name}. Checking base types...");
            
            var baseType = eventType.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                var baseHandlerType = typeof(IBpmnEventHandler<>).MakeGenericType(baseType);
                var baseHandlers = (IEnumerable<object>)_serviceProvider.GetServices(baseHandlerType).ToList();
                Console.WriteLine($"[EventBus] Checking base type {baseType.Name}: Found {baseHandlers.Count()} handlers");
                
                if (baseHandlers.Any())
                {
                    Console.WriteLine($"[EventBus] Using {baseHandlers.Count()} handlers from base type {baseType.Name}");
                    handlers = baseHandlers;
                    handlerInterfaceType = baseHandlerType;
                    break;
                }
                
                baseType = baseType.BaseType;
            }
        }

        if (!handlers.Any())
        {
            Console.WriteLine($"[EventBus] No handlers found for event {eventType.Name} or any of its base types");
            _logger.LogWarning("No registered handlers found for event {EventType}", eventType.Name);
            return;
        }

        foreach (var handler in handlers)
        {
            try
            {
                Console.WriteLine($"[EventBus] Invoking handler: {handler.GetType().Name} for event {eventType.Name}");
                
                // متد HandleAsync
                var handleMethod = handlerInterfaceType.GetMethod("HandleAsync");
                if (handleMethod == null)
                {
                    _logger.LogWarning("Handler {HandlerType} does not have HandleAsync method", handler.GetType().Name);
                    continue;
                }

                // فراخوانی async HandleAsync با پارامترهای رویداد و CancellationToken
                var task = (Task)handleMethod.Invoke(handler, new object[] { @event, cancellationToken });
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EventBus] ERROR invoking handler {handler.GetType().Name}: {ex.Message}");
                _logger.LogError(ex, "Error invoking handler {HandlerType} for event {EventType}", handler.GetType().Name, eventType.Name);
            }
        }
    }

    private async Task HandleWithStreamProcessors(IBpmnEvent @event, CancellationToken cancellationToken)
    {
        foreach (var processor in _processors.Values)
        {
            if (processor.InterestedEventTypes.Any(t => t.IsAssignableFrom(@event.GetType())))
            {
                try
                {
                    await processor.ProcessEventAsync(@event);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing event {EventType} with processor {ProcessorName}", @event.EventType, processor.Name);
                }
            }
        }
    }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IBpmnEvent
    {
        return PublishAsync((IBpmnEvent)@event, cancellationToken);
    }

    public void RegisterProcessor(IStreamProcessor processor)
    {
        if (processor == null) throw new ArgumentNullException(nameof(processor));

        if (_processors.TryAdd(processor.Name, processor))
            _logger.LogInformation("Registered stream processor {ProcessorName}", processor.Name);
        else
            _logger.LogWarning("Stream processor {ProcessorName} already registered", processor.Name);
    }

    public void UnregisterProcessor(string processorName)
    {
        if (string.IsNullOrEmpty(processorName))
            throw new ArgumentException("Processor name must not be empty", nameof(processorName));

        if (_processors.TryRemove(processorName, out _))
            _logger.LogInformation("Unregistered stream processor {ProcessorName}", processorName);
    }
}
