using System;

namespace Novin.Bpmn.EventSourcing.Contracts;

/// <summary>
/// پردازشگر جریان رویدادها در معماری Event Sourcing
/// </summary>
public interface IStreamProcessor
{
    /// <summary>
    /// دریافت نام این پردازشگر
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// دریافت نوع رویدادهایی که این پردازشگر به آنها علاقه‌مند است
    /// </summary>
    IEnumerable<Type> InterestedEventTypes { get; }
    
    /// <summary>
    /// پردازش یک رویداد
    /// </summary>
    /// <param name="event">رویداد مورد نظر</param>
    /// <returns>وظیفه (Task)</returns>
    Task ProcessEventAsync(IBpmnEvent @event);
    
    /// <summary>
    /// شروع پردازش جریان رویدادها
    /// </summary>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وظیفه (Task)</returns>
    Task StartAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// توقف پردازش جریان رویدادها
    /// </summary>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وظیفه (Task)</returns>
    Task StopAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// پردازش مجدد رویدادهای قبلی از یک نقطه زمانی خاص
    /// </summary>
    /// <param name="fromTimestamp">زمان شروع پردازش مجدد</param>
    /// <param name="toTimestamp">زمان پایان پردازش مجدد</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>تعداد رویدادهای پردازش شده</returns>
    Task<int> ReprocessEventsAsync(DateTime fromTimestamp, DateTime? toTimestamp = null, CancellationToken cancellationToken = default);
} 