using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Events;

namespace Novin.Bpmn.Engine.Application.EventHandlers.BoundryEvents;

/// <summary>
/// Infrastructure handler: After subscription is cancelled, unschedule Quartz job.
/// Runs OUTSIDE the transaction that cancelled the subscription (via Outbox).
/// </summary>
public sealed class BoundarySubscriptionCancelledEventHandler : INotificationHandler<BoundarySubscriptionCancelledEvent>
{
    private readonly ITimerScheduler _timerScheduler;
    private readonly ILogger<BoundarySubscriptionCancelledEventHandler> _logger;

    public BoundarySubscriptionCancelledEventHandler(
        ITimerScheduler timerScheduler,
        ILogger<BoundarySubscriptionCancelledEventHandler> logger)
    {
        _timerScheduler = timerScheduler ?? throw new ArgumentNullException(nameof(timerScheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(BoundarySubscriptionCancelledEvent notification, CancellationToken ct)
    {
        try
        {
            await _timerScheduler.UnscheduleAsync(notification.SubscriptionId, ct);
            _logger.LogInformation("Unscheduled timer for cancelled subscription {SubscriptionId}", 
                notification.SubscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unschedule timer for subscription {SubscriptionId}", 
                notification.SubscriptionId);
            // Don't throw - subscription is already cancelled
        }
    }
}

