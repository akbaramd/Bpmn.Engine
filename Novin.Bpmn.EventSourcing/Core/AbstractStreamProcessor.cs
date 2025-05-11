using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// Base class for all event stream processors
/// </summary>
public abstract class AbstractStreamProcessor
{
    private readonly string _processorName;
    private readonly IEventStore _eventStore;
    private readonly IEventBus _eventBus;
    private readonly ILogger _logger;
    private readonly HashSet<Type> _interestedEventTypes = new();
    private long _lastProcessedPosition = -1;
    private CancellationTokenSource? _processingCts;
    private bool _isRunning;
    private string? _subscriptionId;
    
    /// <summary>
    /// Last processed event position
    /// </summary>
    public long LastProcessedPosition => _lastProcessedPosition;
    
    /// <summary>
    /// Whether the processor is currently running
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets the event store instance
    /// </summary>
    protected IEventStore EventStore => _eventStore;

    /// <summary>
    /// Gets the processor name
    /// </summary>
    protected string ProcessorName => _processorName;
    
    /// <summary>
    /// Creates a new stream processor instance
    /// </summary>
    protected AbstractStreamProcessor(
        string processorName,
        IEventStore eventStore,
        IEventBus eventBus,
        ILogger logger)
    {
        _processorName = processorName ?? throw new ArgumentNullException(nameof(processorName));
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Registers an event type that this processor is interested in
    /// </summary>
    protected void RegisterInterestedEventType<T>() where T : IBpmnEvent
    {
        _interestedEventTypes.Add(typeof(T));
    }
    
    /// <summary>
    /// Checks if the processor is interested in a specific event type
    /// </summary>
    protected bool IsInterestedIn(Type eventType)
    {
        return _interestedEventTypes.Contains(eventType);
    }
    
    /// <summary>
    /// Checks if the processor is interested in a specific event
    /// </summary>
    protected bool IsInterestedIn(IBpmnEvent @event)
    {
        return IsInterestedIn(@event.GetType());
    }
    
    /// <summary>
    /// Starts the processor and replays historical events
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("Stream processor {ProcessorName} is already running", _processorName);
            return;
        }
        
        _logger.LogInformation("Starting stream processor {ProcessorName}", _processorName);
        
        try
        {
            _processingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            // First, replay historical events
            var events = await _eventStore.ReadEventsAsync(
                position: _lastProcessedPosition + 1,
                count: 1000,
                predicate: evt => IsInterestedIn(evt),
                cancellationToken: _processingCts.Token);

            foreach (var evt in events)
            {
                try
                {
                    if (evt.Position > _lastProcessedPosition)
                    {
                        await HandleEventAsync(evt);
                        _lastProcessedPosition = evt.Position;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{ProcessorName} failed to process historical event {EventType} at position {Position}",
                        _processorName, evt.GetType().Name, evt.Position);
                    throw; // Re-throw to prevent processing more events after a failure
                }
            }
            
            // Then subscribe to new events
            _subscriptionId = await _eventStore.SubscribeToEventsAsync(
                handler: async (evt) => 
                {
                    try 
                    {
                        if (IsInterestedIn(evt) && evt.Position > _lastProcessedPosition)
                        {
                            await HandleEventAsync(evt);
                            _lastProcessedPosition = evt.Position;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "{ProcessorName} failed to process event {EventType} at position {Position}",
                            _processorName, evt.GetType().Name, evt.Position);
                        throw; // Re-throw to prevent processing more events after a failure
                    }
                },
                predicate: evt => IsInterestedIn(evt),
                position: _lastProcessedPosition + 1,
                cancellationToken: _processingCts.Token);
                
            _isRunning = true;
            
            _logger.LogInformation("{ProcessorName} started successfully with subscription {SubscriptionId}",
                _processorName, _subscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start stream processor {ProcessorName}", _processorName);
            await CleanupResourcesAsync();
            throw;
        }
    }
    
    /// <summary>
    /// Stops the processor
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            _logger.LogWarning("Stream processor {ProcessorName} is not running", _processorName);
            return;
        }
        
        _logger.LogInformation("Stopping stream processor {ProcessorName}", _processorName);
        
        try
        {
            await CleanupResourcesAsync();
            _isRunning = false;
            _logger.LogInformation("{ProcessorName} stopped successfully", _processorName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop stream processor {ProcessorName}", _processorName);
            throw;
        }
    }

    /// <summary>
    /// Cleans up resources used by the processor
    /// </summary>
    private async Task CleanupResourcesAsync()
    {
        if (!string.IsNullOrEmpty(_subscriptionId))
        {
            await _eventStore.UnsubscribeAsync(_subscriptionId);
            _subscriptionId = null;
        }
        
        if (_processingCts != null)
        {
            _processingCts.Cancel();
            _processingCts.Dispose();
            _processingCts = null;
        }
    }
    
    /// <summary>
    /// Processes an event
    /// </summary>
    protected abstract Task HandleEventAsync(IBpmnEvent @event);
}