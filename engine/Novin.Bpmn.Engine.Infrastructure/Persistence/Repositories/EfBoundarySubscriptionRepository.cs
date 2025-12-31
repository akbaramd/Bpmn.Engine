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

    public async Task<BoundaryEventSubscription?> GetByIdAsync(Guid subscriptionId, CancellationToken ct = default)
    {
        return await _context.BoundaryEventSubscription
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);
    }

    public async Task<IEnumerable<BoundaryEventSubscription>> GetByTokenIdAsync(Guid tokenId, CancellationToken ct = default)
    {
        return await _context.BoundaryEventSubscription
            .Where(s => s.TokenId == tokenId)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<BoundaryEventSubscription>> GetByProcessIdAsync(Guid processId, CancellationToken ct = default)
    {
        return await _context.BoundaryEventSubscription
            .Where(s => s.ProcessId == processId)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<BoundaryEventSubscription>> GetActiveByAttachedElementAsync(
        Guid processId,
        string attachedToElementId,
        CancellationToken ct = default)
    {
        return await _context.BoundaryEventSubscription
            .Where(s => s.ProcessId == processId
                        && s.HostElementId == attachedToElementId
                        && s.State == SubscriptionState.Active)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<BoundaryEventSubscription>> GetActiveByActivityInstanceAsync(
        Guid activityInstanceId,
        CancellationToken ct = default)
    {
        return await _context.BoundaryEventSubscription
            .Where(s => s.ActivityInstanceId == activityInstanceId
                        && s.State == SubscriptionState.Active)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<BoundaryEventSubscription>> GetActiveTimersDueBeforeAsync(
        DateTimeOffset dueBefore,
        CancellationToken ct = default)
    {
        return await _context.BoundaryEventSubscription
            .Where(s => s.Kind == BoundaryKind.Timer
                        && s.State == SubscriptionState.Active
                        && s.DueAt.HasValue
                        && s.DueAt <= dueBefore)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<BoundaryEventSubscription>> GetByCorrelationKeyAsync(
        string correlationKey,
        CancellationToken ct = default)
    {
        return await _context.BoundaryEventSubscription
            .Where(s => s.CorrelationKey == correlationKey
                        && s.State == SubscriptionState.Active)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<BoundaryEventSubscription>> GetActiveErrorSubscriptionsByErrorCodeAsync(
        Guid processId,
        Guid nodeInstnaceId,
        Guid activityInsanationId,
        CancellationToken ct = default)
    {
        return await _context.BoundaryEventSubscription
            .Where(s => s.ProcessId == processId && s.ActivityInstanceId == activityInsanationId && s.NodeInstanceId == nodeInstnaceId
                        && s.Kind == BoundaryKind.Error
                        && s.State == SubscriptionState.Active
                       ) // null = catches all errors ("Any")
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<BoundaryEventSubscription>> GetActiveErrorSubscriptionsByErrorCodeAndElementAsync(
        Guid processId,
        string errorCode,
        string attachedToElementId,
        CancellationToken ct = default)
    {
        return await _context.BoundaryEventSubscription
            .Where(s => s.ProcessId == processId
                        && s.Kind == BoundaryKind.Error
                        && s.State == SubscriptionState.Active
                        && s.HostElementId == attachedToElementId
                        && (s.ErrorCode == errorCode || s.ErrorCode == null)) // null = catches all errors ("Any")
            .ToListAsync(ct);
    }

    public async Task AddAsync(BoundaryEventSubscription subscription, CancellationToken ct = default)
    {
        await _context.BoundaryEventSubscription.AddAsync(subscription, ct);
        _logger.LogDebug(
            "[BOUNDARY-SUBSCRIPTION-REPO] Subscription added. SubscriptionId={SubscriptionId} ProcessId={ProcessId} TokenId={TokenId}",
            subscription.Id,
            subscription.ProcessId,
            subscription.TokenId);
    }

    public Task UpdateAsync(BoundaryEventSubscription subscription, CancellationToken ct = default)
    {
        _context.BoundaryEventSubscription.Update(subscription);
        _logger.LogDebug(
            "[BOUNDARY-SUBSCRIPTION-REPO] Subscription updated. SubscriptionId={SubscriptionId} State={State}",
            subscription.Id,
            subscription.State);
        return Task.CompletedTask;
    }
    
    
}
