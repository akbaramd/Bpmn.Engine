using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Novin.Bpmn.Engine.Application.Services.Implementations.Infrastructure;

/// <summary>
/// Null implementation for testing/development when Quartz is not configured.
/// </summary>
public sealed class NullTimerScheduler : ITimerScheduler
{
    private readonly ILogger<NullTimerScheduler> _logger;

    public NullTimerScheduler(ILogger<NullTimerScheduler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task ScheduleOnceAsync(Guid subscriptionId, DateTimeOffset fireAt, CancellationToken ct = default)
    {
        _logger.LogWarning("NullTimerScheduler: ScheduleOnce called for {SubscriptionId} at {FireAt}. Quartz not configured!", 
            subscriptionId, fireAt);
        return Task.CompletedTask;
    }

    public Task ScheduleIntervalAsync(Guid subscriptionId, DateTimeOffset startAt, TimeSpan interval, CancellationToken ct = default)
    {
        _logger.LogWarning("NullTimerScheduler: ScheduleInterval called for {SubscriptionId} starting at {StartAt} with interval {Interval}. Quartz not configured!", 
            subscriptionId, startAt, interval);
        return Task.CompletedTask;
    }

    public Task RescheduleAsync(Guid subscriptionId, DateTimeOffset nextFireAt, CancellationToken ct = default)
    {
        _logger.LogWarning("NullTimerScheduler: Reschedule called for {SubscriptionId} to {NextFireAt}. Quartz not configured!", 
            subscriptionId, nextFireAt);
        return Task.CompletedTask;
    }

    public Task UnscheduleAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        _logger.LogDebug("NullTimerScheduler: Unschedule called for {SubscriptionId}", subscriptionId);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        return Task.FromResult(false);
    }
}

