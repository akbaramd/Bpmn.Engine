using Microsoft.Extensions.Logging;
using Quartz;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Quartz.NET implementation of IBoundaryTimerScheduler
/// برای زمان‌بندی Timer Boundary Events
/// </summary>
public sealed class QuartzBoundaryTimerScheduler : IBoundaryTimerScheduler
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<QuartzBoundaryTimerScheduler> _logger;

    public QuartzBoundaryTimerScheduler(
        ISchedulerFactory schedulerFactory,
        ILogger<QuartzBoundaryTimerScheduler> logger)
    {
        _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> ScheduleAsync(Guid subscriptionId, DateTimeOffset dueAt, CancellationToken ct)
    {
        var scheduler = await _schedulerFactory.GetScheduler(ct);

        // Create job key: "boundary-timer-{subscriptionId}"
        var jobKey = new JobKey($"boundary-timer-{subscriptionId}", "bpmn-boundary-events");
        
        // Create trigger key: "boundary-timer-trigger-{subscriptionId}"
        var triggerKey = new TriggerKey($"boundary-timer-trigger-{subscriptionId}", "bpmn-boundary-events");

        // Create job with subscriptionId as job data
        var job = JobBuilder.Create<BoundaryTimerJob>()
            .WithIdentity(jobKey)
            .UsingJobData("SubscriptionId", subscriptionId.ToString())
            .Build();

        // Create trigger that fires at dueAt time
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .StartAt(dueAt.DateTime)
            .Build();

        // Schedule job
        await scheduler.ScheduleJob(job, trigger, ct);

        // Return job key as external job key (format: "group.name")
        var externalJobKey = $"{jobKey.Group}.{jobKey.Name}";

        _logger.LogInformation(
            "[QUARTZ-SCHEDULER] Scheduled boundary timer. SubscriptionId={SubscriptionId} DueAt={DueAt} JobKey={JobKey}",
            subscriptionId,
            dueAt,
            externalJobKey);

        return externalJobKey;
    }

    public async Task CancelAsync(string externalJobKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(externalJobKey))
        {
            _logger.LogWarning("[QUARTZ-SCHEDULER] ExternalJobKey is empty. Cannot cancel.");
            return;
        }

        var scheduler = await _schedulerFactory.GetScheduler(ct);

        // Parse job key from "group.name" format
        var parts = externalJobKey.Split('.');
        if (parts.Length != 2)
        {
            _logger.LogWarning(
                "[QUARTZ-SCHEDULER] Invalid job key format. Expected 'group.name', got '{JobKey}'",
                externalJobKey);
            return;
        }

        var jobKey = new JobKey(parts[1], parts[0]);
        var deleted = await scheduler.DeleteJob(jobKey, ct);

        if (deleted)
        {
            _logger.LogInformation(
                "[QUARTZ-SCHEDULER] Canceled boundary timer job. JobKey={JobKey}",
                externalJobKey);
        }
        else
        {
            _logger.LogWarning(
                "[QUARTZ-SCHEDULER] Job not found for cancellation. JobKey={JobKey}",
                externalJobKey);
        }
    }
}