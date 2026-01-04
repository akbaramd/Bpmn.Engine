using Microsoft.EntityFrameworkCore;
using Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Outbox;

/// <summary>
/// Entity Framework implementation of outbox message repository.
/// </summary>
public class EfOutboxMessageRepository : IOutboxMessageRepository
{
    private readonly BpmnEngineDbContext _context;

    public EfOutboxMessageRepository(BpmnEngineDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(OutboxMessage message, CancellationToken ct)
    {
        await _context.OutboxMessages.AddAsync(message, ct);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(OutboxMessage message, CancellationToken ct)
    {
        _context.OutboxMessages.Update(message);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxMessage>> GetExpiredLocksAsync(DateTime currentTime, int batchSize, CancellationToken ct)
    {
        var expiredMessages = await _context.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Processing
                       && m.LockedUntilUtc.HasValue
                       && m.LockedUntilUtc.Value < currentTime)
            .OrderBy(m => m.LockedUntilUtc)
            .Take(batchSize)
            .ToListAsync(ct);

        return expiredMessages.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxMessage>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var messages = await _context.OutboxMessages
            .Where(m => ids.Contains(m.Id))
            .ToListAsync(ct);

        return messages.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxMessage>> GetPendingForRetryAsync(DateTime currentTime, int batchSize, CancellationToken ct)
    {
        var messages = await _context.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Failed
                       && m.NextAttemptOnUtc.HasValue
                       && m.NextAttemptOnUtc.Value <= currentTime)
            .OrderBy(m => m.NextAttemptOnUtc)
            .Take(batchSize)
            .ToListAsync(ct);

        return messages.AsReadOnly();
    }

    public async Task<OutboxMessage> GetByIdAsync(Guid outboxId, CancellationToken ct)
    {
       return await _context.OutboxMessages
           
            .FirstOrDefaultAsync(c=>c.Id == outboxId,ct);

    }


    
public Task MarkProcessedAsync(Guid id, DateTime processedOnUtc, CancellationToken ct)
{
    return _context.OutboxMessages
        .Where(x => x.Id == id)
        .ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, OutboxMessageStatus.Processed)
            .SetProperty(x => x.ProcessedOnUtc, processedOnUtc)
            .SetProperty(x => x.LastError, (string?)null),
            ct);
}

public Task MarkFailedAsync(Guid id, string error, CancellationToken ct)
{
    return _context.OutboxMessages
        .Where(x => x.Id == id)
        .ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, OutboxMessageStatus.Failed)
            .SetProperty(x => x.LastError, error)
            .SetProperty(x => x.LockId, (Guid?)null)
            .SetProperty(x => x.LockedUntilUtc, (DateTime?)null),
            ct);
}

public Task MarkDispatchedAsync(Guid id, DateTime utcNow, CancellationToken ct)
{
    return _context.OutboxMessages
        .Where(x => x.Id == id)
        .ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, OutboxMessageStatus.Dispatched),
            ct);
}

public Task MarkDispatchedAsync(List<Guid> ids, DateTime utcNow, CancellationToken ct)
{
    return _context.OutboxMessages
        .Where(x => ids.Contains(x.Id))
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, OutboxMessageStatus.Dispatched), ct);
}

    
}