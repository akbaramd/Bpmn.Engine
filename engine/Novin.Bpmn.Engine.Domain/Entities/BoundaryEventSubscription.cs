using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;

public sealed class BoundaryEventSubscription : BaseAggregateRoot
{
    public Guid ProcessId { get; private set; }
    public Guid TokenId { get; private set; }

    // ✅ اگر node-instance داری، این خیلی ارزشمند است
    public Guid? NodeInstanceId { get; private set; }

    // Element ID that boundary is attached to (host activity)
    public string HostElementId { get; private set; } = default!;

    // Boundary event element id in BPMN
    public string BoundaryElementId { get; private set; } = default!;

    public BoundaryKind Kind { get; private set; }
    public bool IsInterrupting { get; private set; }
    public SubscriptionState State { get; private set; }

    public DateTimeOffset? DueAt { get; private set; }
    public string? ExternalJobKey { get; private set; }

    public string? CorrelationKey { get; private set; }
    public string? ErrorCode { get; private set; }

    public Guid? ActivityInstanceId { get; private set; }
    public Guid? TokenScopeId { get; private set; }

    // Timer-specific fields (only for Kind == Timer)
    public TimerType? TimerType { get; private set; }
    public string? TimerExpression { get; private set; } // BPMN expression (timeDuration, timeDate, timeCycle)
    public DateTimeOffset? NextDueAtUtc { get; private set; } // For cycle timers
    public DateTime? LastFiredAtUtc { get; private set; }
    public int FireCount { get; private set; }

    public int Version { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? TriggeredAtUtc { get; private set; }
    public DateTime? CanceledAtUtc { get; private set; }

    // Meta: برای Tracing/Debug/UI - فقط داده‌های غیر hot-path
    // داده‌های hot-path باید ستون‌های نرمالایز شده باشند
    public MetaBag Meta { get; private set; } = MetaBag.Empty;

    private BoundaryEventSubscription() { } // EF

    public static BoundaryEventSubscription Create(
        Guid processId,
        Guid tokenId,
        Guid nodeInstanceId ,
        string hostElementId,
        string boundaryElementId,
        BoundaryKind kind,
        bool isInterrupting,

        DateTimeOffset? dueAt = null,
        string? correlationKey = null,
        string? errorCode = null,
        Guid? activityInstanceId = null,
        Guid? tokenScopeId = null,
        MetaBag? meta = null,
        // Timer-specific
        TimerType? timerType = null,
        string? timerExpression = null,
        DateTimeOffset? nextDueAtUtc = null)
    {
        if (processId == Guid.Empty) throw new ArgumentException("ProcessId cannot be empty", nameof(processId));
        if (tokenId == Guid.Empty) throw new ArgumentException("TokenId cannot be empty", nameof(tokenId));
        if (string.IsNullOrWhiteSpace(hostElementId)) throw new ArgumentException("HostElementId cannot be empty", nameof(hostElementId));
        if (string.IsNullOrWhiteSpace(boundaryElementId)) throw new ArgumentException("BoundaryElementId cannot be empty", nameof(boundaryElementId));

        var s = new BoundaryEventSubscription
        {
            Id = Guid.NewGuid(),
            ProcessId = processId,
            TokenId = tokenId,
            NodeInstanceId = nodeInstanceId,
            HostElementId = hostElementId.Trim(),
            BoundaryElementId = boundaryElementId.Trim(),
            Kind = kind,
            IsInterrupting = isInterrupting,
            State = SubscriptionState.Active,
            DueAt = dueAt,
            CorrelationKey = correlationKey,
            ErrorCode = errorCode,
            ActivityInstanceId = activityInstanceId,
            TokenScopeId = tokenScopeId,
            TimerType = timerType,
            TimerExpression = timerExpression?.Trim(),
            NextDueAtUtc = nextDueAtUtc,
            LastFiredAtUtc = null,
            FireCount = 0,
            Meta = meta ?? MetaBag.Empty,
            Version = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        s.AddDomainEvent(new BoundarySubscriptionCreatedEvent(
            SubscriptionId: s.Id,
            ProcessId: s.ProcessId,
            TokenId: s.TokenId,
            ElementId: s.HostElementId,
            BoundaryElementId: s.BoundaryElementId,
            ErrorCode: s.ErrorCode,
            MessageName: null,
            IsErrorHandler: s.Kind == BoundaryKind.Error,
            IsMessageHandler: s.Kind == BoundaryKind.Message,
            OccurredAtUtc: DateTime.UtcNow));

        return s;
    }

    public void MarkTriggered(string? reason = null)
    {
        if (State != SubscriptionState.Active)
            throw new InvalidOperationException($"Cannot trigger in {State}. Must be Active.");

        State = SubscriptionState.Triggered;
        TriggeredAtUtc = DateTime.UtcNow;
        Version++;

        AddDomainEvent(new BoundarySubscriptionTriggeredEvent(
            SubscriptionId: Id,
            ProcessId: ProcessId,
            TokenId: TokenId,
            ActivityInstanceId : ActivityInstanceId,
            ElementId: HostElementId,
            BoundaryElementId: BoundaryElementId,
            OccurredAtUtc: TriggeredAtUtc.Value,
            TriggerReason: reason));
    }

    public void Cancel(string? reason = null)
    {
        if (State != SubscriptionState.Active) return;

        State = SubscriptionState.Canceled;
        CanceledAtUtc = DateTime.UtcNow;
        Version++;

        AddDomainEvent(new BoundarySubscriptionCancelledEvent(
            SubscriptionId: Id,
            ProcessId: ProcessId,
            TokenId: TokenId,
            ElementId: HostElementId,
            BoundaryElementId: BoundaryElementId,
            OccurredAtUtc: CanceledAtUtc.Value,
            CancelReason: reason));
    }

    public void SetExternalJobKey(string jobKey)
    {
        if (string.IsNullOrWhiteSpace(jobKey)) throw new ArgumentException("jobKey empty", nameof(jobKey));
        ExternalJobKey = jobKey.Trim();
        Version++;
    }

    public void ClearExternalJobKey()
    {
        ExternalJobKey = null;
        Version++;
    }

    /// <summary>
    /// Sets a meta value for Tracing/Debug/UI purposes.
    /// Only for non-hot-path data. Hot-path data should be normalized columns.
    /// </summary>
    public void SetMeta(string key, string value)
    {
        Meta = Meta.Set(key, value);
        Version++;
    }

    /// <summary>
    /// Removes a meta key.
    /// </summary>
    public void RemoveMeta(string key)
    {
        if (Meta.Has(key))
        {
            Meta = Meta.Remove(key);
            Version++;
        }
    }

    /// <summary>
    /// Merges additional meta values into existing meta.
    /// </summary>
    public void MergeMeta(IReadOnlyDictionary<string, string> additionalValues)
    {
        if (additionalValues is null || additionalValues.Count == 0)
            return;

        Meta = Meta.Merge(additionalValues);
        Version++;
    }

    /// <summary>
    /// Gets a meta value by key.
    /// </summary>
    public string? GetMeta(string key) => Meta.Get(key);

    /// <summary>
    /// Checks if a meta key exists.
    /// </summary>
    public bool HasMeta(string key) => Meta.Has(key);

    // ==================== Timer-specific methods ====================

    /// <summary>
    /// Marks timer as fired (for cycle timers). Increments FireCount and updates LastFiredAtUtc.
    /// Does NOT change State - subscription remains Active for next cycle.
    /// </summary>
    public void MarkFired()
    {
        if (Kind != BoundaryKind.Timer)
            throw new InvalidOperationException("MarkFired can only be called for Timer subscriptions");

        if (State != SubscriptionState.Active)
            throw new InvalidOperationException($"Cannot fire timer in {State}. Must be Active.");

        FireCount++;
        LastFiredAtUtc = DateTime.UtcNow;
        Version++;
    }

    /// <summary>
    /// Sets the next due time for cycle timers.
    /// </summary>
    public void SetNextDueAt(DateTimeOffset nextDueAt)
    {
        if (Kind != BoundaryKind.Timer)
            throw new InvalidOperationException("SetNextDueAt can only be called for Timer subscriptions");

        if (TimerType != Novin.Bpmn.Engine.Domain.ValueObjects.TimerType.TimeCycle)
            throw new InvalidOperationException("SetNextDueAt can only be called for TimeCycle timers");

        NextDueAtUtc = nextDueAt;
        Version++;
    }

    /// <summary>
    /// Updates timer information (for rescheduling).
    /// </summary>
    public void UpdateTimerInfo(DateTimeOffset? dueAt, DateTimeOffset? nextDueAtUtc = null)
    {
        if (Kind != BoundaryKind.Timer)
            throw new InvalidOperationException("UpdateTimerInfo can only be called for Timer subscriptions");

        DueAt = dueAt;
        if (nextDueAtUtc.HasValue)
            NextDueAtUtc = nextDueAtUtc;
        Version++;
    }
}
