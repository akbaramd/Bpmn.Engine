using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Domain.Entities;

    /// <summary>
    /// Runtime subscription برای Boundary Event
    /// وقتی token وارد یک activity می‌شود، subscription‌های boundary event‌های attach شده فعال می‌شوند
    ///
    /// IMPORTANT: These subscriptions are preserved after process completion for:
    /// - Audit trail and execution flow analysis
    /// - Debugging failed processes and error patterns
    /// - Understanding what error handlers were available during execution
    /// - Operational monitoring of boundary event usage
    ///
    /// Use retention policies to clean up old subscription data based on business requirements.
    /// </summary>
public sealed class BoundarySubscription : BaseAggregateRoot
{
    public Guid ProcessId { get; private set; }
    public Guid TokenId { get; private set; }
    
    /// <summary>
    /// Element ID که boundary event به آن attach شده است
    /// </summary>
    public string AttachedToElementId { get; private set; } = default!;
    
    /// <summary>
    /// Boundary Event ID
    /// </summary>
    public string BoundaryEventId { get; private set; } = default!;
    
    /// <summary>
    /// نوع boundary event (Timer, Message, Signal, Error, ...)
    /// </summary>
    public BoundaryKind Kind { get; private set; }
    
    /// <summary>
    /// آیا این boundary event interrupting است؟
    /// از cancelActivity در BPMN model گرفته می‌شود (default: true)
    /// </summary>
    public bool IsInterrupting { get; private set; }
    
    /// <summary>
    /// وضعیت subscription
    /// </summary>
    public SubscriptionState State { get; private set; }
    
    /// <summary>
    /// برای Timer: زمان موعد trigger شدن
    /// </summary>
    public DateTimeOffset? DueAt { get; private set; }
    
    /// <summary>
    /// کلید job در scheduler خارجی (Hangfire JobId, Quartz Key, ...)
    /// </summary>
    public string? ExternalJobKey { get; private set; }
    
    /// <summary>
    /// برای Message/Signal: correlation key
    /// </summary>
    public string? CorrelationKey { get; private set; }
    
    /// <summary>
    /// برای Error: error code
    /// </summary>
    public string? ErrorCode { get; private set; }
    
    /// <summary>
    /// Activity Instance ID - برای cancel کردن همه subscription‌های یک activity instance
    /// این از token.ActivityInstanceId گرفته می‌شود
    /// </summary>
    public Guid? ActivityInstanceId { get; private set; }

    /// <summary>
    /// Token Scope ID - برای correlation در trace-first token semantics
    /// این از token.ScopeId گرفته می‌شود و برای tracking execution cycles مهم است
    /// </summary>
    public Guid? TokenScopeId { get; private set; }
    
    /// <summary>
    /// Version برای optimistic concurrency control
    /// </summary>
    public int Version { get; private set; }
    
    public DateTime CreatedAt { get; private set; }
    public DateTime? TriggeredAt { get; private set; }
    public DateTime? CanceledAt { get; private set; }

    private BoundarySubscription()
    {
        // EF Core
    }

    public BoundarySubscription(
        Guid processId,
        Guid tokenId,
        string attachedToElementId,
        string boundaryEventId,
        BoundaryKind kind,
        bool isInterrupting,
        DateTimeOffset? dueAt = null,
        string? correlationKey = null,
        string? errorCode = null,
        Guid? activityInstanceId = null,
        Guid? tokenScopeId = null)
    {
        if (processId == Guid.Empty)
            throw new ArgumentException("ProcessId cannot be empty", nameof(processId));
        if (tokenId == Guid.Empty)
            throw new ArgumentException("TokenId cannot be empty", nameof(tokenId));
        if (string.IsNullOrWhiteSpace(attachedToElementId))
            throw new ArgumentException("AttachedToElementId cannot be empty", nameof(attachedToElementId));
        if (string.IsNullOrWhiteSpace(boundaryEventId))
            throw new ArgumentException("BoundaryEventId cannot be empty", nameof(boundaryEventId));

        Id = Guid.NewGuid();
        ProcessId = processId;
        TokenId = tokenId;
        AttachedToElementId = attachedToElementId;
        BoundaryEventId = boundaryEventId;
        Kind = kind;
        IsInterrupting = isInterrupting;
        State = SubscriptionState.Active;
        DueAt = dueAt;
        CorrelationKey = correlationKey;
        ErrorCode = errorCode;
        ActivityInstanceId = activityInstanceId;
        TokenScopeId = tokenScopeId;
        Version = 1;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Mark subscription as triggered
    /// </summary>
    public void MarkTriggered()
    {
        if (State != SubscriptionState.Active)
            throw new InvalidOperationException($"Cannot trigger subscription in {State} state. Must be Active.");

        State = SubscriptionState.Triggered;
        TriggeredAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>
    /// Cancel subscription (e.g., activity completed before trigger)
    /// </summary>
    public void Cancel()
    {
        if (State != SubscriptionState.Active)
            return; // Idempotent

        State = SubscriptionState.Canceled;
        CanceledAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>
    /// Set external job key (for timer scheduling)
    /// </summary>
    public void SetExternalJobKey(string jobKey)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
            throw new ArgumentException("JobKey cannot be empty", nameof(jobKey));

        ExternalJobKey = jobKey;
        Version++;
    }

    /// <summary>
    /// Clear external job key (when canceling scheduled job)
    /// </summary>
    public void ClearExternalJobKey()
    {
        ExternalJobKey = null;
        Version++;
    }
}
