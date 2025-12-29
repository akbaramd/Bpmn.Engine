using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Infrastructure.Persistence;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core repository implementing IRepository<NodeInstance>.
/// NodeInstance is an AggregateRoot (IAggregateRoot via BaseAggregateRoot).
/// </summary>
public sealed class NodeInstanceRepository :   INodeInstanceRepository
{
    private readonly BpmnEngineDbContext _db;

    public NodeInstanceRepository(BpmnEngineDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<NodeInstance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) throw new ArgumentException("id cannot be empty.", nameof(id));

        // Aggregate root: return tracked instance (no AsNoTracking)
        return await _db.NodeInstances
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<NodeInstance>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Read-only list: no-tracking is fine
        return await _db.NodeInstances
            .AsNoTracking()
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(NodeInstance aggregate, CancellationToken cancellationToken = default)
    {
        if (aggregate is null) throw new ArgumentNullException(nameof(aggregate));

        await _db.NodeInstances.AddAsync(aggregate, cancellationToken);
    }

    public Task UpdateAsync(NodeInstance aggregate, CancellationToken cancellationToken = default)
    {
        if (aggregate is null) throw new ArgumentNullException(nameof(aggregate));

        // If already tracked, Update is harmless; if detached, it attaches and marks Modified.
        _db.NodeInstances.Update(aggregate);
        return Task.CompletedTask;
    }

   
    public async Task<IReadOnlyList<NodeInstance>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        if (processId == Guid.Empty) throw new ArgumentException("processId cannot be empty.", nameof(processId));

        return await _db.NodeInstances
            .AsNoTracking()
            .Where(x => x.ProcessId == processId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NodeInstance>> GetByTokenIdAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        if (tokenId == Guid.Empty) throw new ArgumentException("tokenId cannot be empty.", nameof(tokenId));

        return await _db.NodeInstances
            .AsNoTracking()
            .Where(x => x.TokenId == tokenId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<NodeInstance?> TryFindOpenAsync(
        Guid processId,
        Guid tokenId,
        string elementId,
        Guid? scopeId,
        Guid? activityInstanceId,
        string? arrivedViaFlowId,
        CancellationToken cancellationToken = default)
    {
        if (processId == Guid.Empty) throw new ArgumentException("processId cannot be empty.", nameof(processId));
        if (tokenId == Guid.Empty) throw new ArgumentException("tokenId cannot be empty.", nameof(tokenId));
        if (string.IsNullOrWhiteSpace(elementId)) throw new ArgumentException("elementId is required.", nameof(elementId));

        elementId = elementId.Trim();
        arrivedViaFlowId = string.IsNullOrWhiteSpace(arrivedViaFlowId) ? null : arrivedViaFlowId.Trim();

        // "Open" = not terminal (Created/Processing/Waiting). Terminal: Completed/Failed/Skipped.
        // If you want "only Waiting", change predicate accordingly.
        return await _db.NodeInstances
            .SingleOrDefaultAsync(x =>
                    x.ProcessId == processId &&
                    x.TokenId == tokenId &&
                    x.ElementId == elementId &&
                    x.ScopeId == scopeId &&
                    x.ActivityInstanceId == activityInstanceId &&
                    x.ArrivedViaFlowId == arrivedViaFlowId &&
                    x.State != NodeState.Completed &&
                    x.State != NodeState.Failed &&
                    x.State != NodeState.Skipped,
                cancellationToken);
    }

    public async Task<NodeInstance?> GetLastAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        if (processId == Guid.Empty) throw new ArgumentException("processId cannot be empty.", nameof(processId));

        // Prefer CompletedAtUtc if present, else CreatedAtUtc as fallback ordering.
        return await _db.NodeInstances
            .AsNoTracking()
            .Where(x => x.ProcessId == processId)
            .OrderByDescending(x => x.CompletedAtUtc ?? x.CreatedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsForElementAsync(Guid processId, string elementId, CancellationToken cancellationToken = default)
    {
        if (processId == Guid.Empty) throw new ArgumentException("processId cannot be empty.", nameof(processId));
        if (string.IsNullOrWhiteSpace(elementId)) throw new ArgumentException("elementId is required.", nameof(elementId));

        elementId = elementId.Trim();

        return await _db.NodeInstances
            .AsNoTracking()
            .AnyAsync(x => x.ProcessId == processId && x.ElementId == elementId, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) throw new ArgumentException("id cannot be empty.", nameof(id));

       
        _db.NodeInstances.Remove(_db.NodeInstances.First(x=>x.Id == id));
    }
}
