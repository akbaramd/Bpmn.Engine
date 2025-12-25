using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;

public sealed class EfBoundarySubscriptionRepository : IBoundarySubscriptionRepository
{
    private readonly BpmnEngineDbContext _context;
    private readonly ILogger<EfBoundarySubscriptionRepository> _logger;

    public EfBoundarySubscriptionRepository(
        BpmnEngineDbContext context,
        ILogger<EfBoundarySubscriptionRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BoundarySubscription?> GetByIdAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        return await _context.BoundarySubscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);
    }

    public async Task<IEnumerable<BoundarySubscription>> GetByTokenIdAsync(Guid tokenId, CancellationToken ct = default)
    {
        return await _context.BoundarySubscriptions
            .Where(s => s.TokenId == tokenId)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<BoundarySubscription>> GetByProcessIdAsync(Guid processId, CancellationToken ct = default)
    {
        return await _context.BoundarySubscriptions
            .Where(s => s.ProcessId == processId)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<BoundarySubscription>> GetActiveByAttachedElementAsync(
        Guid processId,
        string attachedToElementId,
        CancellationToken ct = default)
    {
        return await _context.BoundarySubscriptions
            .Where(s => s.ProcessId == processId
                        && s.AttachedToElementId == attachedToElementId
                        && s.State == SubscriptionState.Active)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<BoundarySubscription>> GetActiveByActivityInstanceAsync(
        Guid activityInstanceId,
        CancellationToken ct = default)
    {
        return await _context.BoundarySubscriptions
            .Where(s => s.ActivityInstanceId == activityInstanceId
                        && s.State == SubscriptionState.Active)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<BoundarySubscription>> GetActiveTimersDueBeforeAsync(
        DateTimeOffset dueBefore,
        CancellationToken ct = default)
    {
        return await _context.BoundarySubscriptions
            .Where(s => s.Kind == BoundaryKind.Timer
                        && s.State == SubscriptionState.Active
                        && s.DueAt.HasValue
                        && s.DueAt <= dueBefore)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<BoundarySubscription>> GetByCorrelationKeyAsync(
        string correlationKey,
        CancellationToken ct = default)
    {
        return await _context.BoundarySubscriptions
            .Where(s => s.CorrelationKey == correlationKey
                        && s.State == SubscriptionState.Active)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<BoundarySubscription>> GetActiveErrorSubscriptionsByErrorCodeAsync(
        Guid processId,
        string errorCode,
        CancellationToken ct = default)
    {
        return await _context.BoundarySubscriptions
            .Where(s => s.ProcessId == processId
                        && s.Kind == BoundaryKind.Error
                        && s.State == SubscriptionState.Active
                        && (s.ErrorCode == errorCode || s.ErrorCode == null)) // null = catches all errors ("Any")
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<BoundarySubscription>> GetActiveErrorSubscriptionsByErrorCodeAndElementAsync(
        Guid processId,
        string errorCode,
        string attachedToElementId,
        CancellationToken ct = default)
    {
        return await _context.BoundarySubscriptions
            .Where(s => s.ProcessId == processId
                        && s.Kind == BoundaryKind.Error
                        && s.State == SubscriptionState.Active
                        && s.AttachedToElementId == attachedToElementId
                        && (s.ErrorCode == errorCode || s.ErrorCode == null)) // null = catches all errors ("Any")
            .ToListAsync(ct);
    }

    public async Task AddAsync(BoundarySubscription subscription, CancellationToken ct = default)
    {
        await _context.BoundarySubscriptions.AddAsync(subscription, ct);
        _logger.LogDebug(
            "[BOUNDARY-SUBSCRIPTION-REPO] Subscription added. SubscriptionId={SubscriptionId} ProcessId={ProcessId} TokenId={TokenId}",
            subscription.Id,
            subscription.ProcessId,
            subscription.TokenId);
    }

    public Task UpdateAsync(BoundarySubscription subscription, CancellationToken ct = default)
    {
        _context.BoundarySubscriptions.Update(subscription);
        _logger.LogDebug(
            "[BOUNDARY-SUBSCRIPTION-REPO] Subscription updated. SubscriptionId={SubscriptionId} State={State}",
            subscription.Id,
            subscription.State);
        return Task.CompletedTask;
    }
}
