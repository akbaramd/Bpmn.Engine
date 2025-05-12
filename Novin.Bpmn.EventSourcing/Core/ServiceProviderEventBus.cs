using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// پیاده‌سازی گذرگاه رویداد با استفاده از تزریق وابستگی
/// </summary>
public class ServiceProviderEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServiceProviderEventBus> _logger;
    
    // مجموعه‌ای از اشتراک‌ها برای هر نوع رویداد
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, Delegate>> _subscriptions = 
        new ConcurrentDictionary<Type, ConcurrentDictionary<string, Delegate>>();

    /// <summary>
    /// ایجاد یک نمونه جدید از گذرگاه رویداد
    /// </summary>
    /// <param name="serviceProvider">ارائه‌دهنده سرویس</param>
    /// <param name="logger">سیستم ثبت وقایع</param>
    public ServiceProviderEventBus(
        IServiceProvider serviceProvider,
        ILogger<ServiceProviderEventBus> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task PublishAsync(IBpmnEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));

        _logger.LogDebug("Publishing event {EventType} with ID {EventId} for process instance {ProcessInstanceId}",
            @event.EventType, @event.EventId, @event.InstanceId);

        // رسیدگی به اشتراک‌های مستقیم
        var eventType = @event.GetType();
        if (_subscriptions.TryGetValue(eventType, out var eventSubscriptions))
        {
            var subscriberTasks = new List<Task>();
            foreach (var subscription in eventSubscriptions)
            {
                try
                {
                    // فراخوانی هندلر اشتراک
                    var handlerTask = (Task)subscription.Value.DynamicInvoke(@event);
                    subscriberTasks.Add(handlerTask);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error invoking subscription handler for event {EventType}",
                        @event.EventType);
                }
            }
            
            // اجرای همه اشتراک‌ها
            if (subscriberTasks.Any())
            {
                await Task.WhenAll(subscriberTasks);
            }
        }
        
        // همچنین اشتراک‌های IBpmnEvent عمومی را نیز فراخوانی می‌کنیم
        if (_subscriptions.TryGetValue(typeof(IBpmnEvent), out var genericSubscriptions))
        {
            var subscriberTasks = new List<Task>();
            foreach (var subscription in genericSubscriptions)
            {
                try
                {
                    // فراخوانی هندلر اشتراک
                    var handlerTask = (Task)subscription.Value.DynamicInvoke(@event);
                    subscriberTasks.Add(handlerTask);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error invoking generic subscription handler for event {EventType}",
                        @event.EventType);
                }
            }
            
            // اجرای همه اشتراک‌ها
            if (subscriberTasks.Any())
            {
                await Task.WhenAll(subscriberTasks);
            }
        }

        // یافتن نوع واسط هندلر با استفاده از نوع رویداد
        var handlerType = typeof(IBpmnEventHandler<>).MakeGenericType(eventType);

        // دریافت تمام هندلرها از تزریق وابستگی
        _logger.LogDebug("Attempting to resolve handlers for type {HandlerType}", handlerType.FullName);
        var handlers = _serviceProvider.GetServices(handlerType);

        if (!handlers.Any())
        {
            _logger.LogWarning("No registered handlers found for event {EventType} with handler type {HandlerType}. " +
                              "Check that your handlers are properly registered in dependency injection.", 
                              @event.EventType, handlerType.FullName);
            
            // برای کمک به عیب‌یابی، تلاش می‌کنیم همه سرویس‌های ثبت شده را شناسایی کنیم
            // اما این در محیط تولید توصیه نمی‌شود
            _logger.LogDebug("Trying to find if any IBpmnEventHandler<> implementations are registered");
            try 
            {
                var anyHandlerType = typeof(IBpmnEventHandler<>);
                var registeredTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .Where(t => !t.IsAbstract && !t.IsInterface && t.IsClass)
                    .Where(t => t.GetInterfaces()
                                .Any(i => i.IsGenericType && 
                                         i.GetGenericTypeDefinition() == anyHandlerType))
                    .ToList();
                
                if (registeredTypes.Any())
                {
                    _logger.LogDebug("Found {Count} handler implementations in loaded assemblies:", registeredTypes.Count);
                    foreach (var type in registeredTypes.Take(10)) // محدود کردن به 10 مورد برای جلوگیری از لاگ بیش از حد
                    {
                        _logger.LogDebug("- {TypeName} implements {Interfaces}", 
                            type.FullName,
                            string.Join(", ", type.GetInterfaces()
                                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == anyHandlerType)
                                .Select(i => i.ToString())));
                    }
                }
                else
                {
                    _logger.LogWarning("No IBpmnEventHandler<> implementations found in loaded assemblies");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while trying to find handler implementations");
            }
            
            return;
        }

        // ساخت و فراخوانی متد HandleAsync برای هر هندلر
        _logger.LogDebug("Found {Count} handlers for event {EventType}", handlers.Count(), @event.EventType);

        // اجرای غیرهمزمان تمام هندلرها
        var tasks = new List<Task>();
        foreach (var handler in handlers)
        {
            try
            {
                var task = (Task)handler.GetType().GetMethod("HandleAsync").Invoke(handler, new object[] { @event, cancellationToken });
                tasks.Add(task);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invoking event handler {HandlerType} for event {EventType}",
                    handler.GetType().Name, @event.EventType);
            }
        }

        // انتظار برای تکمیل همه تسک‌ها
        if (tasks.Any())
        {
            await Task.WhenAll(tasks);
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IBpmnEvent
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));

        _logger.LogDebug("Publishing strongly-typed event {EventType} with ID {EventId} for process instance {ProcessInstanceId}",
            @event.EventType, @event.EventId, @event.InstanceId);

        // رسیدگی به اشتراک‌های نوع خاص
        if (_subscriptions.TryGetValue(typeof(TEvent), out var eventSubscriptions))
        {
            var subscriberTasks = new List<Task>();
            foreach (var subscription in eventSubscriptions)
            {
                try
                {
                    // فراخوانی هندلر اشتراک با نوع قوی
                    var handlerFunc = (Func<TEvent, Task>)subscription.Value;
                    subscriberTasks.Add(handlerFunc(@event));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error invoking typed subscription handler for event {EventType}",
                        @event.EventType);
                }
            }
            
            // اجرای همه اشتراک‌ها
            if (subscriberTasks.Any())
            {
                await Task.WhenAll(subscriberTasks);
            }
        }

        // دریافت تمام هندلرهای قوی‌کتایب از تزریق وابستگی
        var handlers = _serviceProvider.GetServices<IBpmnEventHandler<TEvent>>();

        if (!handlers.Any())
        {
            _logger.LogDebug("No handlers found for event {EventType}", @event.EventType);
            
            // در صورتی که هندلر قوی تایپ نداشته باشیم، از طریق متد غیر تایپ فراخوانی می‌کنیم
            await PublishAsync((IBpmnEvent)@event, cancellationToken);
            return;
        }

        // اجرای غیرهمزمان تمام هندلرها
        var tasks = new List<Task>();
        foreach (var handler in handlers)
        {
            try
            {
                tasks.Add(handler.HandleAsync(@event, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invoking strongly-typed event handler {HandlerType} for event {EventType}",
                    handler.GetType().Name, @event.EventType);
            }
        }

        // انتظار برای تکمیل همه تسک‌ها
        if (tasks.Any())
        {
            await Task.WhenAll(tasks);
        }
    }
    
    /// <inheritdoc />
    public string Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : IBpmnEvent
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
            
        // ایجاد شناسه یکتا برای اشتراک
        var subscriptionId = Guid.NewGuid().ToString();
        
        // ثبت هندلر برای نوع رویداد
        var subscriptions = _subscriptions.GetOrAdd(typeof(TEvent), _ => 
            new ConcurrentDictionary<string, Delegate>());
            
        subscriptions[subscriptionId] = handler;
        
        _logger.LogDebug("Added subscription {SubscriptionId} for event type {EventType}", 
            subscriptionId, typeof(TEvent).Name);
            
        return subscriptionId;
    }
    
    /// <inheritdoc />
    public void Unsubscribe<TEvent>() where TEvent : IBpmnEvent
    {
        // حذف تمام اشتراک‌ها برای نوع رویداد
        _subscriptions.TryRemove(typeof(TEvent), out _);
        
        _logger.LogDebug("Removed all subscriptions for event type {EventType}", 
            typeof(TEvent).Name);
    }
    
    /// <inheritdoc />
    public void Unsubscribe(string subscriptionId)
    {
        if (string.IsNullOrEmpty(subscriptionId))
            throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
            
        // جستجو در تمام اشتراک‌ها و حذف شناسه مورد نظر
        foreach (var eventType in _subscriptions.Keys)
        {
            if (_subscriptions.TryGetValue(eventType, out var subscriptions))
            {
                if (subscriptions.TryRemove(subscriptionId, out _))
                {
                    _logger.LogDebug("Removed subscription {SubscriptionId} for event type {EventType}", 
                        subscriptionId, eventType.Name);
                    return;
                }
            }
        }
        
        _logger.LogWarning("Subscription {SubscriptionId} not found", subscriptionId);
    }
} 