using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Infrastructure.EventBus;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence;

/// <summary>
/// Implementation of transaction service using DbContext directly.
/// Handles transactions and ensures proper change tracking and causality.
/// </summary>
public sealed class TransactionService : ITransactionService
{
    private readonly BpmnEngineDbContext _context;
    private readonly DomainEventDispatcher _domainEventDispatcher;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        BpmnEngineDbContext context,
        DomainEventDispatcher domainEventDispatcher,
        ILogger<TransactionService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _domainEventDispatcher = domainEventDispatcher ?? throw new ArgumentNullException(nameof(domainEventDispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        IDbContextTransaction? transaction = null;
        try
        {
            transaction = await _context.Database.BeginTransactionAsync(ct);
            await action(ct);
            
            // Save changes with domain events (handles causality properly)
            await SaveChangesWithEventsAsync(ct);
            
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TRANSACTION] Transaction failed. Rolling back.");
            if (transaction != null)
            {
                try
                {
                    await transaction.RollbackAsync(ct);
                }
                catch (Exception rbEx)
                {
                    _logger.LogError(rbEx, "[TRANSACTION] Rollback failed.");
                }
            }
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task<int> SaveChangesWithEventsAsync(CancellationToken cancellationToken = default)
    {
        // This algorithm ensures proper causality:
        // - Collects domain events from tracked entities
        // - Clears events from entities
        // - Saves changes to database
        // - Dispatches events (which may create new changes/events)
        // - Repeats until no more changes or events remain
        var total = 0;

        while (true)
        {
            // Collect domain events from tracked aggregates
            var domainEntities = _context.ChangeTracker
                .Entries<IAggregateRoot>()
                .Where(x => x.Entity.DomainEvents.Any())
                .Select(x => x.Entity)
                .ToList();

            var domainEvents = domainEntities
                .SelectMany(x => x.DomainEvents)
                .ToList();

            // Clear events from entities before saving
            domainEntities.ForEach(e => e.ClearDomainEvents());

            var hasChanges = _context.ChangeTracker.HasChanges();
            if (!hasChanges && domainEvents.Count == 0)
                break;

            // 1) Persist changes to database
            if (hasChanges)
            {
                total += await _context.SaveChangesAsync(cancellationToken);
                _logger.LogDebug(
                    "[SAVE-CHANGES] Saved {Count} changes. Total={Total}",
                    total,
                    total);
            }

            // 2) Dispatch events (may create new changes/events)
            if (domainEvents.Count > 0)
            {
                _logger.LogDebug(
                    "[SAVE-CHANGES] Dispatching {Count} domain events",
                    domainEvents.Count);
                await _domainEventDispatcher.DispatchEventsAsync(domainEvents, cancellationToken);
            }
        }

        return total;
    }
}
