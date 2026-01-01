namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Scheduler interface for BPMN timer boundary events.
/// Abstracts Quartz/Hangfire/etc. for testability.
/// </summary>
public interface ITimerScheduler
{
    /// <summary>
    /// Schedules a one-shot timer (TimeDate or TimeDuration).
    /// </summary>
    Task ScheduleOnceAsync(Guid subscriptionId, DateTimeOffset fireAt, CancellationToken ct = default);

    /// <summary>
    /// Schedules a repeating timer (TimeCycle) with interval.
    /// </summary>
    Task ScheduleIntervalAsync(Guid subscriptionId, DateTimeOffset startAt, TimeSpan interval, CancellationToken ct = default);

    /// <summary>
    /// Reschedules an existing timer to a new fire time.
    /// </summary>
    Task RescheduleAsync(Guid subscriptionId, DateTimeOffset nextFireAt, CancellationToken ct = default);

    /// <summary>
    /// Unschedule/delete a timer.
    /// </summary>
    Task UnscheduleAsync(Guid subscriptionId, CancellationToken ct = default);

    /// <summary>
    /// Checks if a timer job exists.
    /// </summary>
    Task<bool> ExistsAsync(Guid subscriptionId, CancellationToken ct = default);
}

