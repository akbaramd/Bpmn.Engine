using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// پایه برای همه پردازشگران جریان رویداد
/// </summary>
public abstract class AbstractStreamProcessor
{
    private readonly string _processorName;
    private readonly IEventStore _eventStore;
    private readonly IEventBus _eventBus;
    private readonly ILogger _logger;
    private readonly HashSet<Type> _interestedEventTypes = new HashSet<Type>();
    private long _lastProcessedPosition = -1;
    private CancellationTokenSource? _processingCts;
    private bool _isRunning = false;
    private string? _subscriptionId;
    
    /// <summary>
    /// آخرین موقعیت پردازش شده
    /// </summary>
    public long LastProcessedPosition => _lastProcessedPosition;
    
    /// <summary>
    /// آیا پردازشگر در حال اجراست
    /// </summary>
    public bool IsRunning => _isRunning;
    
    /// <summary>
    /// ایجاد نمونه پردازشگر جریان
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
    /// ثبت نوع رویداد موردعلاقه
    /// </summary>
    protected void RegisterInterestedEventType<T>() where T : IBpmnEvent
    {
        _interestedEventTypes.Add(typeof(T));
    }
    
    /// <summary>
    /// بررسی اینکه آیا نوع رویداد موردعلاقه است یا خیر
    /// </summary>
    protected bool IsInterestedIn(Type eventType)
    {
        return _interestedEventTypes.Contains(eventType);
    }
    
    /// <summary>
    /// بررسی اینکه آیا رویداد موردعلاقه است یا خیر
    /// </summary>
    protected bool IsInterestedIn(IBpmnEvent @event)
    {
        return IsInterestedIn(@event.GetType());
    }
    
    /// <summary>
    /// شروع پردازشگر و بازیابی رویدادهای قبلی
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
            // ایجاد یک CancellationTokenSource متصل به توکن ورودی
            _processingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            // اشتراک در IEventBus برای دریافت رویدادهای فعلی و آینده
            // این روش از پردازش رویدادهای جدید در زمان واقعی اطمینان حاصل می‌کند
            _eventBus.Subscribe<IBpmnEvent>(async (evt) => 
            {
                try 
                {
                    if (IsInterestedIn(evt))
                    {
                        _logger.LogDebug("{ProcessorName} processing event {EventType} for instance {ProcessId}",
                            _processorName, evt.EventType, evt.ProcessInstanceId);
                            
                        await HandleEventAsync(evt);
                        
                        // بروزرسانی وضعیت پردازش پس از هر رویداد
                        if (evt.Position > _lastProcessedPosition)
                        {
                            _lastProcessedPosition = evt.Position;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{ProcessorName} failed to process event {EventType} for instance {ProcessId}",
                        _processorName, evt.EventType, evt.ProcessInstanceId);
                }
            });
            
            // بررسی رویدادهای تاریخی نیز انجام شود
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
                        _logger.LogError(ex, "{ProcessorName} failed to process historical event {EventType} at position {Position}",
                            _processorName, evt.EventType, evt.Position);
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
            
            // اطمینان از آزادسازی منابع در صورت خطا
            if (_processingCts != null)
            {
                _processingCts.Dispose();
                _processingCts = null;
            }
            
            throw;
        }
    }
    
    /// <summary>
    /// توقف پردازشگر
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
            // لغو اشتراک از مخزن رویداد
            if (!string.IsNullOrEmpty(_subscriptionId))
            {
                await _eventStore.UnsubscribeAsync(_subscriptionId, cancellationToken);
                _subscriptionId = null;
            }
            
            // لغو اشتراک از گذرگاه رویداد
            _eventBus.Unsubscribe<IBpmnEvent>();
            
            // لغو پردازش رویدادهای جدید
            if (_processingCts != null)
            {
                _processingCts.Cancel();
                _processingCts.Dispose();
                _processingCts = null;
            }
            
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
    /// پردازش رویداد
    /// </summary>
    protected abstract Task HandleEventAsync(IBpmnEvent @event);
}