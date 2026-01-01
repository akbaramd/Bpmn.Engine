using Quartz;
using Quartz.Impl.Matchers;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Novin.Bpmn.Engine.Application.Services.Implementations.Infrastructure;

/// <summary>
/// Quartz-based implementation of ITimerScheduler for BPMN timer boundary events.
/// Uses deterministic job/trigger keys: ("bpmn.boundary.timer", subscriptionId.ToString())
/// </summary>
public sealed class QuartzTimerScheduler : ITimerScheduler
{
    private readonly IScheduler _scheduler;
    private readonly ILogger<QuartzTimerScheduler> _logger;

    private const string JobGroup = "bpmn.boundary.timer";
    private const string TriggerGroup = "bpmn.boundary.timer.trigger";

    public QuartzTimerScheduler(IScheduler scheduler, ILogger<QuartzTimerScheduler> logger)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ScheduleOnceAsync(Guid subscriptionId, DateTimeOffset fireAt, CancellationToken ct = default)
    {
        var jobKey = CreateJobKey(subscriptionId);
        var triggerKey = CreateTriggerKey(subscriptionId);

        // Idempotent: delete existing if any
        if (await _scheduler.CheckExists(jobKey, ct))
        {
            _logger.LogDebug("Job {JobKey} already exists, deleting before reschedule", jobKey);
            await _scheduler.DeleteJob(jobKey, ct);
        }

        var job = JobBuilder.Create<BoundaryTimerJob>()
            .WithIdentity(jobKey)
            .UsingJobData("SubscriptionId", subscriptionId.ToString())
            .StoreDurably(false) // Auto-delete when no triggers
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .StartAt(fireAt.DateTime)
            .WithSimpleSchedule(x => x
                .WithMisfireHandlingInstructionFireNow()) // Execute ASAP if missed
            .Build();

        await _scheduler.ScheduleJob(job, trigger, ct);
        _logger.LogInformation("Scheduled one-shot timer for subscription {SubscriptionId} at {FireAt}", 
            subscriptionId, fireAt);
    }

    public async Task ScheduleIntervalAsync(Guid subscriptionId, DateTimeOffset startAt, TimeSpan interval, CancellationToken ct = default)
    {
        var jobKey = CreateJobKey(subscriptionId);
        var triggerKey = CreateTriggerKey(subscriptionId);

        // Idempotent: delete existing if any
        if (await _scheduler.CheckExists(jobKey, ct))
        {
            _logger.LogDebug("Job {JobKey} already exists, deleting before reschedule", jobKey);
            await _scheduler.DeleteJob(jobKey, ct);
        }

        var job = JobBuilder.Create<BoundaryTimerJob>()
            .WithIdentity(jobKey)
            .UsingJobData("SubscriptionId", subscriptionId.ToString())
            .StoreDurably(false)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .StartAt(startAt.DateTime)
            .WithSimpleSchedule(x => x
                .WithInterval(interval)
                .RepeatForever()
                .WithMisfireHandlingInstructionFireNow())
            .Build();

        await _scheduler.ScheduleJob(job, trigger, ct);
        _logger.LogInformation("Scheduled interval timer for subscription {SubscriptionId} starting at {StartAt} with interval {Interval}", 
            subscriptionId, startAt, interval);
    }

    public async Task RescheduleAsync(Guid subscriptionId, DateTimeOffset nextFireAt, CancellationToken ct = default)
    {
        var triggerKey = CreateTriggerKey(subscriptionId);

        if (!await _scheduler.CheckExists(triggerKey, ct))
        {
            _logger.LogWarning("Trigger {TriggerKey} does not exist for reschedule, creating new", triggerKey);
            // Fallback to ScheduleOnce if trigger doesn't exist
            await ScheduleOnceAsync(subscriptionId, nextFireAt, ct);
            return;
        }

        var newTrigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .StartAt(nextFireAt.DateTime)
            .WithSimpleSchedule(x => x
                .WithMisfireHandlingInstructionFireNow())
            .Build();

        await _scheduler.RescheduleJob(triggerKey, newTrigger, ct);
        _logger.LogInformation("Rescheduled timer for subscription {SubscriptionId} to {NextFireAt}", 
            subscriptionId, nextFireAt);
    }

    public async Task UnscheduleAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        var jobKey = CreateJobKey(subscriptionId);

        if (await _scheduler.CheckExists(jobKey, ct))
        {
            await _scheduler.DeleteJob(jobKey, ct);
            _logger.LogInformation("Unscheduled timer for subscription {SubscriptionId}", subscriptionId);
        }
        else
        {
            _logger.LogDebug("Job {JobKey} does not exist, nothing to unschedule", jobKey);
        }
    }

    public async Task<bool> ExistsAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        var jobKey = CreateJobKey(subscriptionId);
        return await _scheduler.CheckExists(jobKey, ct);
    }

    private static JobKey CreateJobKey(Guid subscriptionId) => new(subscriptionId.ToString(), JobGroup);
    private static TriggerKey CreateTriggerKey(Guid subscriptionId) => new(subscriptionId.ToString(), TriggerGroup);
}

