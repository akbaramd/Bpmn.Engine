using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// کلاس پایه برای تمام پردازش‌کننده‌های رویداد BPMN
/// </summary>
/// <typeparam name="TEvent">نوع رویداد</typeparam>
public abstract class BaseEventHandler<TEvent> : IBpmnEventHandler<TEvent> where TEvent : IBpmnEvent
{
    /// <summary>
    /// سیستم ثبت وقایع
    /// </summary>
    protected readonly ILogger Logger;
    
    /// <summary>
    /// مخزن وضعیت
    /// </summary>
    protected readonly IStateStore StateStore;
    
    /// <summary>
    /// گذرگاه رویداد
    /// </summary>
    protected readonly IEventBus EventBus;

    /// <summary>
    /// ایجاد یک نمونه جدید از کلاس پایه پردازش‌کننده رویداد
    /// </summary>
    /// <param name="logger">سیستم ثبت وقایع</param>
    /// <param name="stateStore">مخزن وضعیت</param>
    /// <param name="eventBus">گذرگاه رویداد</param>
    protected BaseEventHandler(
        ILogger logger,
        IStateStore stateStore,
        IEventBus eventBus)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        StateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    /// <inheritdoc />
    public virtual async Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            // پیش از پردازش رویداد
            await BeforeHandleAsync(@event, cancellationToken);
            
            // پردازش اصلی رویداد (پیاده‌سازی توسط کلاس‌های فرزند)
            await ProcessEventAsync(@event, cancellationToken);
            
            // پس از پردازش رویداد
            await AfterHandleAsync(@event, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling event {EventType} for process instance {ProcessInstanceId}",
                @event.EventType, @event.ProcessInstanceId);
            throw;
        }
    }

    /// <summary>
    /// عملیات قبل از پردازش رویداد
    /// </summary>
    /// <param name="event">رویداد</param>
    /// <param name="cancellationToken">توکن لغو</param>
    protected virtual Task BeforeHandleAsync(TEvent @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// پردازش اصلی رویداد
    /// </summary>
    /// <param name="event">رویداد</param>
    /// <param name="cancellationToken">توکن لغو</param>
    protected abstract Task ProcessEventAsync(TEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// عملیات بعد از پردازش رویداد
    /// </summary>
    /// <param name="event">رویداد</param>
    /// <param name="cancellationToken">توکن لغو</param>
    protected virtual Task AfterHandleAsync(TEvent @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
} 