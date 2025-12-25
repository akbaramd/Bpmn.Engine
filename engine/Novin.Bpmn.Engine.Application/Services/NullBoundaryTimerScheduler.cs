using Microsoft.Extensions.Logging;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Null implementation - برای حالتی که scheduler نداریم یا testing
/// فقط log می‌کند و job key fake برمی‌گرداند
/// </summary>
public sealed class NullBoundaryTimerScheduler : IBoundaryTimerScheduler
{
    private readonly ILogger<NullBoundaryTimerScheduler> _logger;

    public NullBoundaryTimerScheduler(ILogger<NullBoundaryTimerScheduler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<string> ScheduleAsync(Guid subscriptionId, DateTimeOffset dueAt, CancellationToken ct)
    {
        _logger.LogWarning(
            "[TIMER-SCHEDULER] Null scheduler: Timer boundary event NOT scheduled. SubscriptionId={SubscriptionId} DueAt={DueAt}",
            subscriptionId,
            dueAt);
        
        // Return fake key - در production باید Hangfire/Quartz استفاده شود
        return Task.FromResult($"null-{subscriptionId}");
    }

    public Task CancelAsync(string externalJobKey, CancellationToken ct)
    {
        _logger.LogDebug(
            "[TIMER-SCHEDULER] Null scheduler: Timer cancellation ignored. JobKey={JobKey}",
            externalJobKey);
        
        return Task.CompletedTask;
    }
}
