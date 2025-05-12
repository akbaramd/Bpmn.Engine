using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Novin.Bpmn.EventSourcing.Contracts;
using System.Collections.Generic;
using System.Linq;

namespace Novin.Bpmn.EventSourcing.Core;

public class EventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventBus> _logger;
    private readonly ConcurrentDictionary<Type, List<Type>> _eventHandlers = new();
    private readonly ConcurrentDictionary<string, IStreamProcessor> _processors = new();

    public EventBus(IServiceProvider serviceProvider, ILogger<EventBus> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        InitializeEventHandlers();
    }

    private void InitializeEventHandlers()
    {
        try
        {
            _logger.LogInformation("Initializing event handlers...");
            
            // Find all event handler implementations
            var handlerTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IBpmnEventHandler<>)))
                .ToList();

            foreach (var handlerType in handlerTypes)
            {
                var eventType = handlerType.GetInterfaces()
                    .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IBpmnEventHandler<>))
                    .GetGenericArguments()[0];

                var handlers = _eventHandlers.GetOrAdd(eventType, _ => new List<Type>());
                handlers.Add(handlerType);
                
                _logger.LogDebug("Registered handler {HandlerType} for event {EventType}", 
                    handlerType.Name, eventType.Name);
            }
            
            _logger.LogInformation("Event handler initialization completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during event handler initialization");
            throw;
        }
    }

    public async Task PublishAsync(IBpmnEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));
        
        _logger.LogDebug("Publishing event {@EventType} for process {ProcessInstanceId}",
            @event.EventType, @event.InstanceId);
        
        try
        {
            // 1. First handle with registered event handlers
            await HandleWithRegisteredHandlers(@event, cancellationToken);
            
            // 2. Then handle with registered stream processors
            await HandleWithStreamProcessors(@event, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing event {@EventType} for process {ProcessInstanceId}",
                @event.EventType, @event.InstanceId);
            throw;
        }
    }

    private async Task HandleWithRegisteredHandlers(IBpmnEvent @event, CancellationToken cancellationToken)
    {
        var eventType = @event.GetType();
        
        if (_eventHandlers.TryGetValue(eventType, out var handlerTypes))
        {
            foreach (var handlerType in handlerTypes)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var handler = scope.ServiceProvider.GetService(handlerType);
                    
                    if (handler == null)
                    {
                        _logger.LogWarning("Could not resolve handler of type {HandlerType}", handlerType.Name);
                        continue;
                    }
                    
                    var method = handlerType.GetMethod("HandleAsync");
                    if (method != null)
                    {
                        await (Task)method.Invoke(handler, new object[] { @event, cancellationToken });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error invoking handler {HandlerType} for event {EventType}",
                        handlerType.Name, eventType.Name);
                }
            }
        }
    }

    private async Task HandleWithStreamProcessors(IBpmnEvent @event, CancellationToken cancellationToken)
    {
        // Process the event with all interested stream processors
        foreach (var processor in _processors.Values)
        {
            // Check if the processor is interested in this event type
            if (processor.InterestedEventTypes.Any(t => t.IsAssignableFrom(@event.GetType())))
            {
                try
                {
                    await processor.ProcessEventAsync(@event);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing event {@EventType} with processor {ProcessorName}",
                        @event.EventType, processor.Name);
                }
            }
        }
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) 
        where TEvent : IBpmnEvent
    {
        await PublishAsync((IBpmnEvent)@event, cancellationToken);
    }
    
    /// <summary>
    /// Register a stream processor with the event bus
    /// </summary>
    /// <param name="processor">The processor to register</param>
    public void RegisterProcessor(IStreamProcessor processor)
    {
        if (processor == null)
            throw new ArgumentNullException(nameof(processor));
            
        if (_processors.TryAdd(processor.Name, processor))
        {
            _logger.LogInformation("Registered stream processor {ProcessorName}", processor.Name);
        }
        else
        {
            _logger.LogWarning("Stream processor {ProcessorName} already registered", processor.Name);
        }
    }
    
    /// <summary>
    /// Unregister a stream processor from the event bus
    /// </summary>
    /// <param name="processorName">The name of the processor to unregister</param>
    public void UnregisterProcessor(string processorName)
    {
        if (string.IsNullOrEmpty(processorName))
            throw new ArgumentException("Processor name must not be empty", nameof(processorName));
            
        if (_processors.TryRemove(processorName, out _))
        {
            _logger.LogInformation("Unregistered stream processor {ProcessorName}", processorName);
        }
    }
} 