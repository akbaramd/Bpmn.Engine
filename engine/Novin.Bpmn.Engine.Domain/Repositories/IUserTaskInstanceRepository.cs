using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Domain.Repositories;


/// Repository interface for UserTaskInstance aggregate.
/// </summary>
public interface IUserTaskInstanceRepository : IRepository<UserTaskInstance>
{
    Task<IEnumerable<UserTaskInstance?>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserTaskInstance?>> GetByStatusAsync(UserTaskStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the latest task for a token (if multiple exist due to retries, cancellations, etc.).
    /// </summary>
    Task<UserTaskInstance?> GetByTokenIdAsync(Guid tokenId, CancellationToken cancellationToken = default);

    Task<UserTaskInstance?> GetByTokenAndElementAsync(Guid tokenId, string elementId, CancellationToken cancellationToken = default);

    Task UpdateAsync(UserTaskInstance? userTask, CancellationToken cancellationToken = default);
    Task<UserTaskInstance?> GetByKeyAsync(Guid processId, Guid tokenId, Guid nodeInstanceId, string elementId, CancellationToken ct);
}
