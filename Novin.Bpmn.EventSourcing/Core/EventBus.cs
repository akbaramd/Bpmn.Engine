using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Novin.Bpmn.EventSourcing.Contracts;

namespace Novin.Bpmn.EventSourcing.Core;

public class EventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventBus> _logger;
    private readonly ConcurrentDictionary<Type, List<Type>> _eventHandlers = new();

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
                .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>)))
                .ToList();

            foreach (var handlerType in handlerTypes)
            {
                var eventType = handlerType.GetInterfaces()
                    .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
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
        try
        {
            var eventType = @event.GetType();
            _logger.LogDebug("Publishing event {EventType}", eventType.Name);

            if (_eventHandlers.TryGetValue(eventType, out var handlerTypes))
            {
                foreach (var handlerType in handlerTypes)
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var handler = (IEventHandler<IBpmnEvent>)ActivatorUtilities.CreateInstance(scope.ServiceProvider, handlerType);
                        await handler.HandleAsync(@event, null);
                        _logger.LogDebug("Handler {HandlerType} completed successfully", handlerType.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling event {EventType} with handler {HandlerType}", 
                            eventType.Name, handlerType.Name);
                        throw;
                    }
                }
            }
            else
            {
                _logger.LogWarning("No handlers found for event {EventType}", eventType.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing event {EventType}", @event.GetType().Name);
            throw;
        }
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IBpmnEvent
    {
        await PublishAsync((IBpmnEvent)@event, cancellationToken);
    }
} 