using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Contracts;

/// <summary>
/// مخزن ذخیره‌سازی و بازیابی رویدادها
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// ذخیره رویداد جدید
    /// </summary>
    /// <param name="event">رویداد موردنظر</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>موقعیت ذخیره‌شده رویداد</returns>
    Task<long> AppendEventAsync(IBpmnEvent @event, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// ذخیره چندین رویداد جدید به صورت یکپارچه
    /// </summary>
    /// <param name="events">رویدادهای موردنظر</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>موقعیت آخرین رویداد ذخیره‌شده</returns>
    Task<long> AppendEventsAsync(IEnumerable<IBpmnEvent> events, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// خواندن رویدادها از یک موقعیت خاص
    /// </summary>
    /// <param name="position">موقعیت شروع</param>
    /// <param name="count">حداکثر تعداد رویدادها</param>
    /// <param name="predicate">تابع فیلترکننده (اختیاری)</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>لیست رویدادهای خوانده‌شده</returns>
    Task<List<IBpmnEvent>> ReadEventsAsync(
        long position = 0,
        int count = 100,
        Func<IBpmnEvent, bool>? predicate = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// خواندن رویدادهای یک نمونه فرآیند خاص
    /// </summary>
    /// <param name="processInstanceId">شناسه نمونه فرآیند</param>
    /// <param name="position">موقعیت شروع</param>
    /// <param name="count">حداکثر تعداد رویدادها</param>
    /// <param name="predicate">تابع فیلترکننده (اختیاری)</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>لیست رویدادهای خوانده‌شده</returns>
    Task<List<IBpmnEvent>> ReadProcessInstanceEventsAsync(
        string processInstanceId,
        long position = 0,
        int count = 100,
        Func<IBpmnEvent, bool>? predicate = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// اشتراک در رویدادهای جدید
    /// </summary>
    /// <param name="handler">تابع پردازش‌کننده رویداد</param>
    /// <param name="predicate">تابع فیلترکننده (اختیاری)</param>
    /// <param name="position">موقعیت شروع</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>شناسه اشتراک</returns>
    Task<string> SubscribeToEventsAsync(
        Func<IBpmnEvent, Task> handler,
        Func<IBpmnEvent, bool>? predicate = null,
        long position = 0,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// لغو اشتراک رویدادها
    /// </summary>
    /// <param name="subscriptionId">شناسه اشتراک</param>
    /// <param name="cancellationToken">توکن لغو</param>
    Task UnsubscribeAsync(string subscriptionId, CancellationToken cancellationToken = default);
} 