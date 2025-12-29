using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Repository interface for Worker aggregate
/// </summary>
public interface IWorkerRepository : IRepository<Domain.Entities.Worker>
{
    Task<IEnumerable<Worker>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Worker>> GetByStatusAsync(WorkerStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<Worker>> GetByTypeAsync(WorkerType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<Worker>> GetPendingWorkersAsync(string? assignee = null, string? clientId = null, CancellationToken cancellationToken = default);
    Task<Worker?> GetByTokenIdAsync(Guid tokenId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Worker worker, CancellationToken cancellationToken = default);
}