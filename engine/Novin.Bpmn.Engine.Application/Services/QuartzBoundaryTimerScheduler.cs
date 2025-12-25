using Microsoft.Extensions.Logging;
using Quartz;
using MediatR;
using Novin.Bpmn.Engine.Application.Commands.TriggerBoundarySubscription;

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

/// <summary>
/// Quartz Job که وقتی timer trigger می‌شود، TriggerBoundarySubscriptionCommand را اجرا می‌کند
/// </summary>
[DisallowConcurrentExecution]
public sealed class BoundaryTimerJob : IJob
{
    private readonly IMediator _mediator;
    private readonly ILogger<BoundaryTimerJob> _logger;

    public BoundaryTimerJob(
        IMediator mediator,
        ILogger<BoundaryTimerJob> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var subscriptionIdStr = context.JobDetail.JobDataMap.GetString("SubscriptionId");
        if (string.IsNullOrWhiteSpace(subscriptionIdStr) || !Guid.TryParse(subscriptionIdStr, out var subscriptionId))
        {
            _logger.LogError(
                "[BOUNDARY-TIMER-JOB] Invalid SubscriptionId in job data. JobKey={JobKey}",
                context.JobDetail.Key);
            return;
        }

        _logger.LogInformation(
            "[BOUNDARY-TIMER-JOB] Timer triggered. SubscriptionId={SubscriptionId} JobKey={JobKey} FiredAt={FiredAt}",
            subscriptionId,
            context.JobDetail.Key,
            context.FireTimeUtc);

        try
        {
            var command = new TriggerBoundarySubscriptionCommand(subscriptionId);
            var result = await _mediator.Send(command, context.CancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "[BOUNDARY-TIMER-JOB] Boundary subscription triggered successfully. SubscriptionId={SubscriptionId} NewTokenId={NewTokenId}",
                    subscriptionId,
                    result.NewTokenId);
            }
            else
            {
                _logger.LogWarning(
                    "[BOUNDARY-TIMER-JOB] Failed to trigger boundary subscription. SubscriptionId={SubscriptionId} Error={Error}",
                    subscriptionId,
                    result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[BOUNDARY-TIMER-JOB] Exception while triggering boundary subscription. SubscriptionId={SubscriptionId}",
                subscriptionId);
            throw; // Re-throw to let Quartz handle retry logic
        }
    }
}
