using Microsoft.EntityFrameworkCore;
using Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Outbox;

/// <summary>
/// SQL Server implementation of outbox batch claimer using high-performance UPDATE TOP with locking hints.
/// NOTE: This is for SQL Server only. For SQLite development, use SqliteOutboxBatchClaimer.
/// </summary>
public class SqlServerOutboxBatchClaimer : IOutboxBatchClaimer
{
    private readonly BpmnEngineDbContext _context;

    public SqlServerOutboxBatchClaimer(BpmnEngineDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(int batchSize, TimeSpan lease, CancellationToken ct)
    {
        var lockId = Guid.NewGuid();
        var lockedUntilUtc = DateTime.UtcNow.Add(lease);

        // Use raw SQL for high-performance claiming with proper locking
        // This prevents race conditions and ensures only one worker can claim each message
        var claimedMessages = await _context.OutboxMessages
            .FromSqlRaw(@"
                UPDATE TOP (@BatchSize) OutboxMessages WITH (READPAST, UPDLOCK, ROWLOCK)
                SET Status = 1, LockId = @LockId, LockedUntilUtc = @LockedUntilUtc, Attempts = Attempts + 1
                OUTPUT inserted.*
                WHERE Status IN (0, 3)  -- Pending or Failed
                  AND (NextAttemptOnUtc IS NULL OR NextAttemptOnUtc <= @CurrentTime)
                ORDER BY OccurredOnUtc ASC, NextAttemptOnUtc ASC",
                new Microsoft.Data.Sqlite.SqliteParameter("@BatchSize", batchSize),
                new Microsoft.Data.Sqlite.SqliteParameter("@LockId", lockId),
                new Microsoft.Data.Sqlite.SqliteParameter("@LockedUntilUtc", lockedUntilUtc),
                new Microsoft.Data.Sqlite.SqliteParameter("@CurrentTime", DateTime.UtcNow))
            .ToListAsync(ct);

        return claimedMessages.AsReadOnly();
    }
}