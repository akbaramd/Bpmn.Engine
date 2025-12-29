using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Repository برای BoundaryEventSubscription
/// </summary>
public interface IBoundarySubscriptionRepository
{
    Task<BoundaryEventSubscription?> GetByIdAsync(Guid subscriptionId, CancellationToken ct = default);
    Task<IEnumerable<BoundaryEventSubscription>> GetByTokenIdAsync(Guid tokenId, CancellationToken ct = default);
    Task<IEnumerable<BoundaryEventSubscription>> GetByProcessIdAsync(Guid processId, CancellationToken ct = default);
    Task<IEnumerable<BoundaryEventSubscription>> GetActiveByAttachedElementAsync(
        Guid processId, 
        string attachedToElementId, 
        CancellationToken ct = default);
    Task<IEnumerable<BoundaryEventSubscription>> GetActiveByActivityInstanceAsync(
        Guid activityInstanceId, 
        CancellationToken ct = default);
    Task<IEnumerable<BoundaryEventSubscription>> GetActiveTimersDueBeforeAsync(
        DateTimeOffset dueBefore, 
        CancellationToken ct = default);
    Task<IEnumerable<BoundaryEventSubscription>> GetByCorrelationKeyAsync(
        string correlationKey, 
        CancellationToken ct = default);
    /// <summary>
    /// Get active error subscriptions by ErrorCode for a process.
    /// Used for error boundary lookup with scope-aware matching.
    /// </summary>
    Task<IEnumerable<BoundaryEventSubscription>> GetActiveErrorSubscriptionsByErrorCodeAsync(
        Guid processId,
        string errorCode,
        CancellationToken ct = default);

    /// <summary>
    /// Get active error subscriptions by ErrorCode and ElementId for a process.
    /// Used for error boundary lookup when error occurs on specific element.
    /// </summary>
    Task<IEnumerable<BoundaryEventSubscription>> GetActiveErrorSubscriptionsByErrorCodeAndElementAsync(
        Guid processId,
        string errorCode,
        string attachedToElementId,
        CancellationToken ct = default);
    Task AddAsync(BoundaryEventSubscription subscription, CancellationToken ct = default);
    Task UpdateAsync(BoundaryEventSubscription subscription, CancellationToken ct = default);
}
