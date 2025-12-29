// File: Novin.Bpmn.Engine.Infrastructure/Persistence/Repositories/UserTaskInstanceRepository.cs
using Microsoft.EntityFrameworkCore;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Repositories;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;

public sealed class UserTaskInstanceRepository : IUserTaskInstanceRepository
{
    private readonly BpmnEngineDbContext _db;

    public UserTaskInstanceRepository(BpmnEngineDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    // -----------------------------
    // IRepository<UserTaskInstance>
    // -----------------------------

    public async Task<IEnumerable<UserTaskInstance>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.UserTaskInstances
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(UserTaskInstance entity, CancellationToken cancellationToken = default)
    {
        if (entity is null) throw new ArgumentNullException(nameof(entity));
        await _db.UserTaskInstances.AddAsync(entity, cancellationToken);
    }

    public async Task<UserTaskInstance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));

        return await _db.UserTaskInstances
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task RemoveAsync(UserTaskInstance entity, CancellationToken cancellationToken = default)
    {
        if (entity is null) throw new ArgumentNullException(nameof(entity));
        _db.UserTaskInstances.Remove(entity);
        return Task.CompletedTask;
    }

    // -----------------------------
    // IUserTaskInstanceRepository
    // -----------------------------

    public async Task<IEnumerable<UserTaskInstance?>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        if (processId == Guid.Empty) throw new ArgumentException("processId cannot be empty.", nameof(processId));

        return await _db.UserTaskInstances
            .AsNoTracking()
            .Where(x => x.ProcessId == processId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<UserTaskInstance?>> GetByStatusAsync(UserTaskStatus status, CancellationToken cancellationToken = default)
    {
        return await _db.UserTaskInstances
            .AsNoTracking()
            .Where(x => x.Status == status)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserTaskInstance?> GetByTokenIdAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        if (tokenId == Guid.Empty) throw new ArgumentException("tokenId cannot be empty.", nameof(tokenId));

        return await _db.UserTaskInstances
            .Where(x => x.TokenId == tokenId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UserTaskInstance?> GetByTokenAndElementAsync(Guid tokenId, string elementId, CancellationToken cancellationToken = default)
    {
        if (tokenId == Guid.Empty) throw new ArgumentException("tokenId cannot be empty.", nameof(tokenId));
        if (string.IsNullOrWhiteSpace(elementId)) throw new ArgumentException("elementId cannot be empty.", nameof(elementId));

        return await _db.UserTaskInstances
            .FirstOrDefaultAsync(x => x.TokenId == tokenId && x.ElementId == elementId, cancellationToken);
    }

    public Task UpdateAsync(UserTaskInstance? userTask, CancellationToken cancellationToken = default)
    {
        if (userTask is null) throw new ArgumentNullException(nameof(userTask));

        _db.UserTaskInstances.Update(userTask);
        return Task.CompletedTask;
    }

    public async Task<UserTaskInstance?> GetByKeyAsync(
        Guid processId,
        Guid tokenId,
        Guid nodeInstanceId,
        string elementId,
        CancellationToken ct)
    {
        if (processId == Guid.Empty) throw new ArgumentException("processId cannot be empty.", nameof(processId));
        if (tokenId == Guid.Empty) throw new ArgumentException("tokenId cannot be empty.", nameof(tokenId));
        if (nodeInstanceId == Guid.Empty) throw new ArgumentException("nodeInstanceId cannot be empty.", nameof(nodeInstanceId));
        if (string.IsNullOrWhiteSpace(elementId)) throw new ArgumentException("elementId cannot be empty.", nameof(elementId));

        // Key semantics: (ProcessId, TokenId, NodeInstanceId, ElementId)
        return await _db.UserTaskInstances
            .FirstOrDefaultAsync(x =>
                    x.ProcessId == processId &&
                    x.TokenId == tokenId &&
                    x.NodeInstanceId == nodeInstanceId &&
                    x.ElementId == elementId,
                ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) throw new ArgumentException("Id cannot be empty.", nameof(id));

        // Idempotent delete: if not found, no-op.
        var entity = await _db.UserTaskInstances
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return;

        _db.UserTaskInstances.Remove(entity);
    }
}
