namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Abstraction برای زمان‌بندی Timer Boundary Events
/// قابل تعویض: Hangfire امروز، Quartz فردا، ...
/// </summary>
public interface IBoundaryTimerScheduler
{
    /// <summary>
    /// زمان‌بندی یک timer boundary event
    /// </summary>
    /// <param name="subscriptionId">ID subscription</param>
    /// <param name="dueAt">زمان موعد trigger شدن</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>External job key (Hangfire JobId, Quartz Key, ...)</returns>
    Task<string> ScheduleAsync(Guid subscriptionId, DateTimeOffset dueAt, CancellationToken ct);
    
    /// <summary>
    /// لغو یک scheduled timer
    /// </summary>
    /// <param name="externalJobKey">کلید job که از ScheduleAsync برگشته</param>
    /// <param name="ct">Cancellation token</param>
    Task CancelAsync(string externalJobKey, CancellationToken ct);
}
