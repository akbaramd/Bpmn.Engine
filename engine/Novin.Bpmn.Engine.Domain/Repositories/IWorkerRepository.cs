using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Repository interface for Job aggregate
/// </summary>
public interface IWorkerRepository : IRepository<Job>
{
    Task<IEnumerable<Job?>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Job?>> GetByStatusAsync(JobStatus status, CancellationToken cancellationToken = default);
    Task<Job?> GetByTokenIdAsync(Guid tokenId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Job? worker, CancellationToken cancellationToken = default);
    Task<Job?> GetByTokenAndElementAsync(Guid tokenId, string elementId, CancellationToken ct);
}