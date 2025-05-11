using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.EventSourcing.Core.Models;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// Base class for all BPMN event handlers
/// </summary>
/// <typeparam name="TEvent">Type of event to handle</typeparam>
public abstract class BaseEventHandler<TEvent> : IEventHandler<TEvent>, IBpmnEventHandler<TEvent> where TEvent : IBpmnEvent
{
    /// <summary>
    /// Logger instance
    /// </summary>
    protected readonly ILogger Logger;
    
    /// <summary>
    /// State store for process instances
    /// </summary>
    protected readonly IStateStore StateStore;
    
    /// <summary>
    /// Event bus for publishing events
    /// </summary>
    protected readonly IEventBus EventBus;

    /// <summary>
    /// Event store for persisting events
    /// </summary>
    protected readonly IEventStore EventStore;

    /// <summary>
    /// Definition store for BPMN process definitions
    /// </summary>
    protected readonly IDefinitionStore DefinitionStore;

    /// <summary>
    /// Creates a new instance of the base event handler
    /// </summary>
    protected BaseEventHandler(
        IStateStore stateStore,
        IEventStore eventStore,
        IDefinitionStore definitionStore,
        ILogger logger)
    {
        StateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        EventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        DefinitionStore = definitionStore ?? throw new ArgumentNullException(nameof(definitionStore));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public virtual async Task HandleAsync(TEvent @event, BpmnProcessState state, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Handling event {EventType} for process instance {ProcessInstanceId}",
                @event.GetType().Name, @event.ProcessInstanceId);

            // Pre-handling operations
            await BeforeHandleAsync(@event, cancellationToken);
            
            // Main event processing (implemented by derived classes)
            await ProcessEventAsync(@event, cancellationToken);
            
            // Post-handling operations
            await AfterHandleAsync(@event, cancellationToken);

            Logger.LogInformation("Successfully handled event {EventType} for process instance {ProcessInstanceId}",
                @event.GetType().Name, @event.ProcessInstanceId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling event {EventType} for process instance {ProcessInstanceId}",
                @event.GetType().Name, @event.ProcessInstanceId);
            throw;
        }
    }

    /// <summary>
    /// Operations to perform before handling the event
    /// </summary>
    protected virtual Task BeforeHandleAsync(TEvent @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Main event processing logic to be implemented by derived classes
    /// </summary>
    protected abstract Task ProcessEventAsync(TEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Operations to perform after handling the event
    /// </summary>
    protected virtual Task AfterHandleAsync(TEvent @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the current state of a process instance
    /// </summary>
    protected async Task<BpmnProcessState> GetStateAsync(string processInstanceId, CancellationToken cancellationToken)
    {
        var state = await StateStore.GetStateAsync(processInstanceId, cancellationToken);
        if (state == null)
        {
            throw new InvalidOperationException($"Process instance {processInstanceId} not found");
        }
        return state;
    }

    /// <summary>
    /// Saves the state of a process instance
    /// </summary>
    protected async Task SaveStateAsync(string processInstanceId, BpmnProcessState state, long? expectedVersion, CancellationToken cancellationToken)
    {
        try
        {
            await StateStore.SaveStateAsync(processInstanceId, state, expectedVersion, cancellationToken);
            Logger.LogDebug("Saved state for process instance {ProcessInstanceId} with version {Version}",
                processInstanceId, expectedVersion);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save state for process instance {ProcessInstanceId}", processInstanceId);
            throw;
        }
    }

    /// <summary>
    /// Saves an event to the event store
    /// </summary>
    protected async Task SaveEventAsync(IBpmnEvent @event, CancellationToken cancellationToken)
    {
        try
        {
            await EventStore.AppendEventAsync(@event, cancellationToken);
            Logger.LogDebug("Saved event {EventType} for process instance {ProcessInstanceId}",
                @event.GetType().Name, @event.ProcessInstanceId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save event {EventType} for process instance {ProcessInstanceId}", 
                @event.GetType().Name, @event.ProcessInstanceId);
            throw;
        }
    }

    /// <inheritdoc />
    public Task HandleAsync(TEvent @event, BpmnProcessState state)
    {
        return HandleAsync(@event, state, CancellationToken.None);
    }

    /// <inheritdoc />
    public Task HandleAsync(IBpmnEvent @event, BpmnProcessState state)
    {
        if (@event is TEvent typedEvent)
        {
            return HandleAsync(typedEvent, state);
        }
        throw new ArgumentException($"Event type {@event.GetType().Name} is not compatible with handler for {typeof(TEvent).Name}");
    }

    /// <inheritdoc />
    public Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default)
    {
        return HandleAsync(@event, null, cancellationToken);
    }
} 