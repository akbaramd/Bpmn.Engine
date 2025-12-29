using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Generic repository interface for aggregate roots
/// </summary>
public interface IRepository<TAggregate> where TAggregate : IAggregateRoot
{
    Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<TAggregate>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
    Task UpdateAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

