using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.TriggerBoundarySubscription;

/// <summary>
/// Command برای trigger کردن یک Boundary Subscription
/// این command معمولاً از scheduler (Hangfire/Quartz) یا message bus صدا زده می‌شود
/// </summary>
public class TriggerBoundarySubscriptionCommand : IRequest<TriggerBoundarySubscriptionResult>
{
    public Guid SubscriptionId { get; set; }

    public TriggerBoundarySubscriptionCommand(Guid subscriptionId)
    {
        SubscriptionId = subscriptionId;
    }
}