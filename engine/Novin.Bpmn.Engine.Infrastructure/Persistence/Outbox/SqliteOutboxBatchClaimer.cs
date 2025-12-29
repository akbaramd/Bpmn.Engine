using Microsoft.EntityFrameworkCore;
using Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Outbox;

/// <summary>
/// SQLite implementation of outbox batch claimer.
/// SQLite has simpler locking semantics than SQL Server.
/// </summary>
public class SqliteOutboxBatchClaimer : IOutboxBatchClaimer
{
    private readonly BpmnEngineDbContext _context;

    public SqliteOutboxBatchClaimer(BpmnEngineDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(int batchSize, TimeSpan lease, CancellationToken ct)
    {
        var lockId = Guid.NewGuid();
        var lockedUntilUtc = DateTime.UtcNow.Add(lease);
        var currentTime = DateTime.UtcNow;

        // For SQLite, we need a different approach since it doesn't support:
        // - UPDATE TOP with OUTPUT clause
        // - Complex locking hints
        // - OUTPUT inserted.*

        // Step 1: Find candidate messages to claim
        // Order by: oldest messages first (OccurredOnUtc), then by retry priority (NextAttemptOnUtc)
        var candidateIds = await _context.OutboxMessages
            .Where(m => (m.Status == OutboxMessageStatus.Pending || m.Status == OutboxMessageStatus.Failed)
                       && (m.NextAttemptOnUtc == null || m.NextAttemptOnUtc <= currentTime))
            .OrderBy(m => m.OccurredOnUtc)  // Oldest messages first (FIFO - First In, First Out)
            .ThenBy(m => m.NextAttemptOnUtc ?? DateTime.MaxValue)  // Failed messages with earlier retry times get priority
            .Take(batchSize)
            .Select(m => m.Id)
            .ToListAsync(ct);

        if (!candidateIds.Any())
        {
            return Array.Empty<OutboxMessage>();
        }

        // Step 2: Update the messages in a transaction
        // Note: SQLite doesn't have the same concurrency guarantees as SQL Server,
        // but for development/testing this is acceptable
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        try
        {
            // Update the selected messages
            await _context.OutboxMessages
                .Where(m => candidateIds.Contains(m.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.Status, OutboxMessageStatus.Processing)
                    .SetProperty(m => m.LockId, lockId)
                    .SetProperty(m => m.LockedUntilUtc, lockedUntilUtc)
                    .SetProperty(m => m.Attempts, m => m.Attempts + 1),
                    ct);

            await transaction.CommitAsync(ct);

            // Step 3: Return the updated messages
            var claimedMessages = await _context.OutboxMessages
                .Where(m => candidateIds.Contains(m.Id))
                .ToListAsync(ct);

            return claimedMessages.AsReadOnly();
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}