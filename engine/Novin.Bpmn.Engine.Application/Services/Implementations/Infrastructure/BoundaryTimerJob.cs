using Quartz;
using MediatR;
using Novin.Bpmn.Engine.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Novin.Bpmn.Engine.Application.Services.Implementations.Infrastructure;

/// <summary>
/// Quartz job that fires when a BPMN timer boundary event should trigger.
/// Does NOT contain BPMN logic - only publishes BoundarySubscriptionTriggeredEvent.
/// All BPMN decisions (interrupting, cancel, spawn token) happen in BoundarySubscriptionTriggeredEventHandler.
/// </summary>
[DisallowConcurrentExecution] // Prevent duplicate fires for same subscription
public sealed class BoundaryTimerJob : IJob
{
    private readonly IMediator _mediator;
    private readonly ILogger<BoundaryTimerJob> _logger;

    public BoundaryTimerJob(IMediator mediator, ILogger<BoundaryTimerJob> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var subscriptionIdStr = context.MergedJobDataMap.GetString("SubscriptionId");
        if (string.IsNullOrWhiteSpace(subscriptionIdStr) || !Guid.TryParse(subscriptionIdStr, out var subscriptionId))
        {
            _logger.LogError("Invalid SubscriptionId in job data: {SubscriptionId}", subscriptionIdStr);
            throw new JobExecutionException($"Invalid SubscriptionId: {subscriptionIdStr}");
        }

        _logger.LogInformation("Timer fired for subscription {SubscriptionId}", subscriptionId);

        // Publish event - handler will load subscription from DB and execute BPMN logic
        // Note: We don't have full subscription data here, handler will load it
        var @event = new BoundarySubscriptionTriggeredEvent(
            SubscriptionId: subscriptionId,
            ProcessId: Guid.Empty, // Will be loaded by handler
            TokenId: Guid.Empty, // Will be loaded by handler
            ActivityInstanceId: null, // Will be loaded by handler
            ElementId: string.Empty, // Will be loaded by handler
            BoundaryElementId: string.Empty, // Will be loaded by handler
            OccurredAtUtc: DateTime.UtcNow,
            TriggerReason: "Timer");

        await _mediator.Publish(@event, context.CancellationToken);
    }
}

