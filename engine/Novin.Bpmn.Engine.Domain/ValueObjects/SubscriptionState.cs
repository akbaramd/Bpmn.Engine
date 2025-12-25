namespace Novin.Bpmn.Engine.Domain.ValueObjects;

/// <summary>
/// وضعیت Boundary Subscription
/// </summary>
public enum SubscriptionState
{
    /// <summary>
    /// Subscription فعال است و منتظر trigger شدن است
    /// </summary>
    Active,
    
    /// <summary>
    /// Boundary event trigger شده و subscription دیگر فعال نیست
    /// </summary>
    Triggered,
    
    /// <summary>
    /// Subscription لغو شده (مثلاً activity تمام شد قبل از trigger شدن)
    /// </summary>
    Canceled
}
