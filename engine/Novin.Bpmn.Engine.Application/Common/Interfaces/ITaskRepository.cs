using Novin.Bpmn.Engine.Domain.Entities;
using Task = Novin.Bpmn.Engine.Domain.Entities.Task;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Repository interface for Task aggregate
/// </summary>
public interface ITaskRepository : IRepository<Task>
{
    Task<IEnumerable<Task>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default);
    Task<Task?> GetByElementIdAsync(Guid processId, string elementId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Task>> GetByStatusAsync(Guid processId, Domain.ValueObjects.TaskStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<Task>> GetByAssigneeAsync(string assignee, CancellationToken cancellationToken = default);
}

