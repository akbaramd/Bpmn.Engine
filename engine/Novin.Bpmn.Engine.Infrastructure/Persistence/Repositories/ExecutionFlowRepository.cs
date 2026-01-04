using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Infrastructure.Persistence.Ef;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;

public sealed class ExecutionFlowRepository : IExecutionFlowRepository
{
    private readonly BpmnEngineDbContext _db;

    public ExecutionFlowRepository(BpmnEngineDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task AddAsync(ExecutionFlowRecord record, CancellationToken ct)
    {
        await _db.ExecutionFlowRecords.AddAsync(record, ct);
    }

    public async Task<bool> ExistsByEventKeyAsync(string eventKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(eventKey)) return false;
        return await _db.ExecutionFlowRecords.AsNoTracking()
            .AnyAsync(x => x.EventKey == eventKey, ct);
    }

    public async Task<IReadOnlyList<ExecutionFlowRecord>> GetByProcessIdAsync(Guid processId, CancellationToken ct)
    {
        return await _db.ExecutionFlowRecords.AsNoTracking()
            .Where(x => x.ProcessId == processId)
            .OrderBy(x => x.Position)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ExecutionFlowRecord>> GetByTokenIdAsync(Guid tokenId, CancellationToken ct)
    {
        return await _db.ExecutionFlowRecords.AsNoTracking()
            .Where(x => x.TokenId == tokenId)
            .OrderBy(x => x.Position)
            .ToListAsync(ct);
    }

     public async Task<long> GetNextPositionAsync(Guid processId, CancellationToken ct)
    {
        var max = await _db.Set<ExecutionFlowRecord>()
            .Where(x => x.ProcessId == processId)
            .MaxAsync(x => (long?)x.Position, ct);

        return (max ?? 0L) + 1L;
    }
}
