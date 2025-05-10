using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// پیاده‌سازی درون‌حافظه‌ای مخزن رویدادها
/// </summary>
public class InMemoryEventStore : IEventStore
{
    private readonly List<IBpmnEvent> _events = new();
    private readonly object _syncRoot = new();
    private readonly ConcurrentDictionary<string, EventSubscription> _subscriptions = new();
    private readonly ILogger<InMemoryEventStore> _logger;
    private long _nextPosition = 0;
    
    /// <summary>
    /// ایجاد یک نمونه جدید از مخزن رویدادهای درون‌حافظه‌ای
    /// </summary>
    /// <param name="logger">سیستم ثبت وقایع</param>
    public InMemoryEventStore(ILogger<InMemoryEventStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc />
    public Task<long> AppendEventAsync(IBpmnEvent @event, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var position = AppendEventInternal(@event);
        NotifySubscribers(@event);
        
        return Task.FromResult(position);
    }
    
    /// <inheritdoc />
    public Task<long> AppendEventsAsync(IEnumerable<IBpmnEvent> events, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        long lastPosition = -1;
        
        foreach (var @event in events)
        {
            lastPosition = AppendEventInternal(@event);
            cancellationToken.ThrowIfCancellationRequested();
        }
        
        // اعلان به مشترکان برای همه رویدادها
        var eventsArray = events.ToArray();
        foreach (var @event in eventsArray)
        {
            NotifySubscribers(@event);
        }
        
        return Task.FromResult(lastPosition);
    }
    
    /// <inheritdoc />
    public Task<List<IBpmnEvent>> ReadEventsAsync(
        long position = 0,
        int count = 100,
        Func<IBpmnEvent, bool>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        List<IBpmnEvent> result;
        
        lock (_syncRoot)
        {
            var query = _events
                .Where(e => e.Position >= position);
                
            if (predicate != null)
            {
                query = query.Where(predicate);
            }
            
            result = query
                .Take(count)
                .ToList();
        }
        
        return Task.FromResult(result);
    }
    
    /// <inheritdoc />
    public Task<List<IBpmnEvent>> ReadProcessInstanceEventsAsync(
        string processInstanceId,
        long position = 0,
        int count = 100,
        Func<IBpmnEvent, bool>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be null or empty", nameof(processInstanceId));
            
        cancellationToken.ThrowIfCancellationRequested();
        
        List<IBpmnEvent> result;
        
        lock (_syncRoot)
        {
            var query = _events
                .Where(e => e.ProcessInstanceId == processInstanceId && e.Position >= position);
                
            if (predicate != null)
            {
                query = query.Where(predicate);
            }
            
            result = query
                .Take(count)
                .ToList();
        }
        
        return Task.FromResult(result);
    }
    
    /// <inheritdoc />
    public Task<string> SubscribeToEventsAsync(
        Func<IBpmnEvent, Task> handler,
        Func<IBpmnEvent, bool>? predicate = null,
        long position = 0,
        CancellationToken cancellationToken = default)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
            
        // ایجاد یک شناسه یکتا برای اشتراک
        var subscriptionId = Guid.NewGuid().ToString();
        
        var subscription = new EventSubscription
        {
            Id = subscriptionId,
            Handler = handler,
            Predicate = predicate,
            LastProcessedPosition = position - 1 // برای شروع از موقعیت مورد نظر
        };
        
        _subscriptions.TryAdd(subscriptionId, subscription);
        
        _logger.LogInformation("Created event subscription {SubscriptionId} starting from position {Position}", 
            subscriptionId, position);
            
        // اجرای اولیه برای رویدادهای قبلی
        _ = Task.Run(async () => 
        {
            try
            {
                var events = await ReadEventsAsync(position, 1000, predicate, cancellationToken);
                foreach (var @event in events)
                {
                    await subscription.ProcessEventAsync(@event);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during initial subscription processing for {SubscriptionId}", subscriptionId);
            }
        }, cancellationToken);
        
        return Task.FromResult(subscriptionId);
    }
    
    /// <inheritdoc />
    public Task UnsubscribeAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(subscriptionId))
            throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
            
        _subscriptions.TryRemove(subscriptionId, out _);
        
        _logger.LogInformation("Removed event subscription {SubscriptionId}", subscriptionId);
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// افزودن رویداد به فهرست داخلی
    /// </summary>
    private long AppendEventInternal(IBpmnEvent @event)
    {
        // تنظیم موقعیت رویداد
        long position;
        
        lock (_syncRoot)
        {
            position = _nextPosition++;
            
            // تنظیم موقعیت و افزودن به لیست
            var eventWithPosition = @event as dynamic;
            eventWithPosition.Position = position;
            
            _events.Add(@event);
        }
        
        _logger.LogDebug("Appended event {EventType} with ID {EventId} at position {Position}", 
            @event.EventType, @event.EventId, position);
            
        return position;
    }
    
    /// <summary>
    /// اعلان به مشترکان
    /// </summary>
    private void NotifySubscribers(IBpmnEvent @event)
    {
        // ارسال رویداد به همه مشترکان به صورت غیرهمزمان
        foreach (var subscription in _subscriptions.Values)
        {
            // بررسی شرط فیلتر
            if (subscription.Predicate == null || subscription.Predicate(@event))
            {
                // فقط پردازش رویدادهای جدیدتر
                if (@event.Position > subscription.LastProcessedPosition)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await subscription.ProcessEventAsync(@event);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing event {EventId} for subscription {SubscriptionId}", 
                                @event.EventId, subscription.Id);
                        }
                    });
                }
            }
        }
    }
    
    /// <summary>
    /// کلاس داخلی برای نگهداری اطلاعات اشتراک
    /// </summary>
    private class EventSubscription
    {
        /// <summary>
        /// شناسه اشتراک
        /// </summary>
        public string Id { get; set; } = null!;
        
        /// <summary>
        /// تابع پردازش‌کننده رویداد
        /// </summary>
        public Func<IBpmnEvent, Task> Handler { get; set; } = null!;
        
        /// <summary>
        /// تابع فیلترکننده
        /// </summary>
        public Func<IBpmnEvent, bool>? Predicate { get; set; }
        
        /// <summary>
        /// آخرین موقعیت پردازش شده
        /// </summary>
        public long LastProcessedPosition { get; set; }
        
        /// <summary>
        /// پردازش یک رویداد
        /// </summary>
        public async Task ProcessEventAsync(IBpmnEvent @event)
        {
            await Handler(@event);
            
            // بروزرسانی آخرین موقعیت پردازش شده
            LastProcessedPosition = Math.Max(LastProcessedPosition, @event.Position);
        }
    }
} 