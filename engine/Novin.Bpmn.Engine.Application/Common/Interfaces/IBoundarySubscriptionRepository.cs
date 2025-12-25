using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Repository برای BoundarySubscription
/// </summary>
public interface IBoundarySubscriptionRepository
{
    Task<BoundarySubscription?> GetByIdAsync(Guid subscriptionId, CancellationToken ct = default);
    Task<IEnumerable<BoundarySubscription>> GetByTokenIdAsync(Guid tokenId, CancellationToken ct = default);
    Task<IEnumerable<BoundarySubscription>> GetByProcessIdAsync(Guid processId, CancellationToken ct = default);
    Task<IEnumerable<BoundarySubscription>> GetActiveByAttachedElementAsync(
        Guid processId, 
        string attachedToElementId, 
        CancellationToken ct = default);
    Task<IEnumerable<BoundarySubscription>> GetActiveByActivityInstanceAsync(
        Guid activityInstanceId, 
        CancellationToken ct = default);
    Task<IEnumerable<BoundarySubscription>> GetActiveTimersDueBeforeAsync(
        DateTimeOffset dueBefore, 
        CancellationToken ct = default);
    Task<IEnumerable<BoundarySubscription>> GetByCorrelationKeyAsync(
        string correlationKey, 
        CancellationToken ct = default);
    /// <summary>
    /// Get active error subscriptions by ErrorCode for a process.
    /// Used for error boundary lookup with scope-aware matching.
    /// </summary>
    Task<IEnumerable<BoundarySubscription>> GetActiveErrorSubscriptionsByErrorCodeAsync(
        Guid processId,
        string errorCode,
        CancellationToken ct = default);
    Task AddAsync(BoundarySubscription subscription, CancellationToken ct = default);
    Task UpdateAsync(BoundarySubscription subscription, CancellationToken ct = default);
}
