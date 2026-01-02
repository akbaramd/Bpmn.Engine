using Quartz;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Novin.Bpmn.Engine.Application.Services.Implementations.Infrastructure;

/// <summary>
/// Quartz-based implementation of ITimerScheduler for BPMN timer boundary events.
/// Uses deterministic job/trigger keys:
///   JobKey:     (subscriptionId.ToString("N"), "bpmn.boundary.timer")
///   TriggerKey: (subscriptionId.ToString("N"), "bpmn.boundary.timer.trigger")
/// </summary>
public sealed class QuartzTimerScheduler : ITimerScheduler
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<QuartzTimerScheduler> _logger;

    private const string JobGroup = "bpmn.boundary.timer";
    private const string TriggerGroup = "bpmn.boundary.timer.trigger";

    public QuartzTimerScheduler(ISchedulerFactory schedulerFactory, ILogger<QuartzTimerScheduler> logger)
    {
        _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private Task<IScheduler> GetSchedulerAsync(CancellationToken ct) => _schedulerFactory.GetScheduler(ct);

    public async Task ScheduleOnceAsync(Guid subscriptionId, DateTimeOffset fireAt, CancellationToken ct = default)
    {
        var scheduler = await GetSchedulerAsync(ct);

        var jobKey = CreateJobKey(subscriptionId);
        var triggerKey = CreateTriggerKey(subscriptionId);

        // Idempotent: delete existing job (deletes triggers too)
        if (await scheduler.CheckExists(jobKey, ct))
        {
            _logger.LogDebug("Job {JobKey} already exists, deleting before reschedule", jobKey);
            await scheduler.DeleteJob(jobKey, ct);
        }

        var job = JobBuilder.Create<BoundaryTimerJob>()
            .WithIdentity(jobKey)
            .UsingJobData("SubscriptionId", subscriptionId.ToString("N"))
            .StoreDurably(false) // Auto-delete when no triggers
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .StartAt(fireAt) // ✅ keep DateTimeOffset (no timezone loss)
            .WithSimpleSchedule(x => x.WithMisfireHandlingInstructionFireNow())
            .Build();

        await scheduler.ScheduleJob(job, trigger, ct);

        _logger.LogInformation(
            "Scheduled one-shot timer for subscription {SubscriptionId} at {FireAt}",
            subscriptionId, fireAt);
    }

    public async Task ScheduleIntervalAsync(Guid subscriptionId, DateTimeOffset startAt, TimeSpan interval, CancellationToken ct = default)
    {
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));

        var scheduler = await GetSchedulerAsync(ct);

        var jobKey = CreateJobKey(subscriptionId);
        var triggerKey = CreateTriggerKey(subscriptionId);

        if (await scheduler.CheckExists(jobKey, ct))
        {
            _logger.LogDebug("Job {JobKey} already exists, deleting before reschedule", jobKey);
            await scheduler.DeleteJob(jobKey, ct);
        }

        var job = JobBuilder.Create<BoundaryTimerJob>()
            .WithIdentity(jobKey)
            .UsingJobData("SubscriptionId", subscriptionId.ToString("N"))
            .StoreDurably(false)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .StartAt(startAt) // ✅ keep DateTimeOffset
            .WithSimpleSchedule(x => x
                .WithInterval(interval)
                .RepeatForever()
                .WithMisfireHandlingInstructionNowWithExistingCount())
            .Build();

        await scheduler.ScheduleJob(job, trigger, ct);

        _logger.LogInformation(
            "Scheduled interval timer for subscription {SubscriptionId} starting at {StartAt} with interval {Interval}",
            subscriptionId, startAt, interval);
    }

    public async Task RescheduleAsync(Guid subscriptionId, DateTimeOffset nextFireAt, CancellationToken ct = default)
    {
        var scheduler = await GetSchedulerAsync(ct);

        var jobKey = CreateJobKey(subscriptionId);
        var triggerKey = CreateTriggerKey(subscriptionId);

        var existing = await scheduler.GetTrigger(triggerKey, ct);
        if (existing is null)
        {
            _logger.LogWarning("Trigger {TriggerKey} does not exist for reschedule, scheduling once", triggerKey);
            await ScheduleOnceAsync(subscriptionId, nextFireAt, ct);
            return;
        }

        // Preserve schedule type when possible:
        // - If it was SimpleTrigger with interval, we keep interval & repeat count; we only move start time.
        // - Otherwise (one-shot), we reschedule as one-shot.
        if (existing is ISimpleTrigger simple)
        {
            var builder = existing.GetTriggerBuilder()
                .StartAt(nextFireAt);

            // if repeating interval => keep it
            if (simple.RepeatInterval > TimeSpan.Zero && simple.RepeatCount != 0)
            {
                builder = builder.WithSimpleSchedule(x => x
                    .WithInterval(simple.RepeatInterval)
                    .RepeatForever()
                    .WithMisfireHandlingInstructionNowWithExistingCount());
            }
            else
            {
                builder = builder.WithSimpleSchedule(x => x.WithMisfireHandlingInstructionFireNow());
            }

            var newTrigger = builder
                .ForJob(jobKey)
                .Build();

            await scheduler.RescheduleJob(triggerKey, newTrigger, ct);

            _logger.LogInformation(
                "Rescheduled timer for subscription {SubscriptionId} to {NextFireAt}",
                subscriptionId, nextFireAt);
            return;
        }

        // Fallback for other trigger types
        var fallbackTrigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .StartAt(nextFireAt)
            .WithSimpleSchedule(x => x.WithMisfireHandlingInstructionFireNow())
            .Build();

        await scheduler.RescheduleJob(triggerKey, fallbackTrigger, ct);

        _logger.LogInformation(
            "Rescheduled (fallback) timer for subscription {SubscriptionId} to {NextFireAt}",
            subscriptionId, nextFireAt);
    }

    public async Task UnscheduleAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        var scheduler = await GetSchedulerAsync(ct);

        var jobKey = CreateJobKey(subscriptionId);

        if (await scheduler.CheckExists(jobKey, ct))
        {
            await scheduler.DeleteJob(jobKey, ct); // deletes triggers too
            _logger.LogInformation("Unscheduled timer for subscription {SubscriptionId}", subscriptionId);
        }
        else
        {
            _logger.LogDebug("Job {JobKey} does not exist, nothing to unschedule", jobKey);
        }
    }

    public async Task<bool> ExistsAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        var scheduler = await GetSchedulerAsync(ct);
        return await scheduler.CheckExists(CreateJobKey(subscriptionId), ct);
    }

    private static JobKey CreateJobKey(Guid subscriptionId) => new(subscriptionId.ToString("N"), JobGroup);
    private static TriggerKey CreateTriggerKey(Guid subscriptionId) => new(subscriptionId.ToString("N"), TriggerGroup);
}
