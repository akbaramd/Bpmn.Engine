using MediatR;
using Novin.Bpmn.Engine.Application.Commands.TriggerBoundarySubscription;
using Quartz;

namespace Novin.Bpmn.Engine.Application.Services;

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