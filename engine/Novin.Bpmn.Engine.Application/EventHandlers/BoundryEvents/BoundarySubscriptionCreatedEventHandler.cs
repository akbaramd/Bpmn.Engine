using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.EventHandlers.BoundryEvents;

/// <summary>
/// Infrastructure handler: After subscription is committed, schedule Quartz job.
/// Runs OUTSIDE the transaction that created the subscription (via Outbox).
/// </summary>
public sealed class BoundarySubscriptionCreatedEventHandler : INotificationHandler<BoundarySubscriptionCreatedEvent>
{
    private readonly ITimerScheduler _timerScheduler;
    private readonly IBoundarySubscriptionRepository _subscriptionRepository;
    private readonly ILogger<BoundarySubscriptionCreatedEventHandler> _logger;

    public BoundarySubscriptionCreatedEventHandler(
        ITimerScheduler timerScheduler,
        IBoundarySubscriptionRepository subscriptionRepository,
        ILogger<BoundarySubscriptionCreatedEventHandler> logger)
    {
        _timerScheduler = timerScheduler ?? throw new ArgumentNullException(nameof(timerScheduler));
        _subscriptionRepository = subscriptionRepository ?? throw new ArgumentNullException(nameof(subscriptionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(BoundarySubscriptionCreatedEvent notification, CancellationToken ct)
    {
        // Load subscription to get timer details and verify it's a Timer
        var subscription = await _subscriptionRepository.GetByIdAsync(notification.SubscriptionId, ct);
        if (subscription is null)
        {
            _logger.LogWarning("Subscription {SubscriptionId} not found for scheduling", notification.SubscriptionId);
            return;
        }

        if (subscription.Kind != BoundaryKind.Timer)
            return;

        if (subscription.State != SubscriptionState.Active)
        {
            _logger.LogDebug("Subscription {SubscriptionId} is not Active, skipping schedule", notification.SubscriptionId);
            return;
        }

        try
        {
            // Schedule based on timer type
            if (subscription.TimerType == TimerType.TimeDate || subscription.TimerType == TimerType.TimeDuration)
            {
                // One-shot timer
                if (!subscription.DueAt.HasValue)
                {
                    _logger.LogWarning("Timer subscription {SubscriptionId} has no DueAt, cannot schedule", 
                        notification.SubscriptionId);
                    return;
                }

                await _timerScheduler.ScheduleOnceAsync(subscription.Id, subscription.DueAt.Value, ct);
                
                // Update ExternalJobKey after successful schedule
                var jobKey = $"bpmn.boundary.timer/{subscription.Id}";
                subscription.SetExternalJobKey(jobKey);
                await _subscriptionRepository.UpdateAsync(subscription, ct);
            }
            else if (subscription.TimerType == TimerType.TimeCycle)
            {
                // Cycle timer - need interval from expression
                if (!subscription.DueAt.HasValue)
                {
                    _logger.LogWarning("Cycle timer subscription {SubscriptionId} has no DueAt, cannot schedule", 
                        notification.SubscriptionId);
                    return;
                }

                // Parse interval from TimerExpression (simplified - assumes ISO-8601 duration)
                var interval = ParseIntervalFromExpression(subscription.TimerExpression);
                if (!interval.HasValue)
                {
                    _logger.LogWarning("Cannot parse interval from TimerExpression {Expression} for subscription {SubscriptionId}", 
                        subscription.TimerExpression, notification.SubscriptionId);
                    return;
                }

                await _timerScheduler.ScheduleIntervalAsync(subscription.Id, subscription.DueAt.Value, interval.Value, ct);
                
                // Update ExternalJobKey
                var jobKey = $"bpmn.boundary.timer/{subscription.Id}";
                subscription.SetExternalJobKey(jobKey);
                await _subscriptionRepository.UpdateAsync(subscription, ct);
            }

            _logger.LogInformation("Scheduled timer for subscription {SubscriptionId}", notification.SubscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule timer for subscription {SubscriptionId}", notification.SubscriptionId);
            // Don't throw - subscription is already created, we'll retry via recovery
        }
    }

    private static TimeSpan? ParseIntervalFromExpression(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return null;

        // Simple ISO-8601 duration parser (PT5M = 5 minutes, PT2S = 2 seconds)
        // For production, use a proper ISO-8601 parser library
        try
        {
            if (System.Xml.XmlConvert.ToTimeSpan(expression) is var ts && ts != default)
                return ts;
        }
        catch
        {
            // Try manual parsing for common patterns
        }

        // Fallback: try to parse common patterns
        if (expression.StartsWith("PT", StringComparison.OrdinalIgnoreCase))
        {
            var remaining = expression.Substring(2);
            var totalSeconds = 0.0;

            // Extract hours
            var hourIndex = remaining.IndexOf('H');
            if (hourIndex >= 0)
            {
                if (double.TryParse(remaining.Substring(0, hourIndex), out var hours))
                    totalSeconds += hours * 3600;
                remaining = remaining.Substring(hourIndex + 1);
            }

            // Extract minutes
            var minIndex = remaining.IndexOf('M');
            if (minIndex >= 0)
            {
                if (double.TryParse(remaining.Substring(0, minIndex), out var minutes))
                    totalSeconds += minutes * 60;
                remaining = remaining.Substring(minIndex + 1);
            }

            // Extract seconds
            var secIndex = remaining.IndexOf('S');
            if (secIndex >= 0)
            {
                if (double.TryParse(remaining.Substring(0, secIndex), out var seconds))
                    totalSeconds += seconds;
            }

            if (totalSeconds > 0)
                return TimeSpan.FromSeconds(totalSeconds);
        }

        return null;
    }
}

