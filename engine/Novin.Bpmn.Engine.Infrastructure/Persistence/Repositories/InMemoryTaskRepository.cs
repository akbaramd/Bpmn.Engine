using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Task = System.Threading.Tasks.Task;
using EntityTask = Novin.Bpmn.Engine.Domain.Entities.Task;
using TaskStatus = Novin.Bpmn.Engine.Domain.ValueObjects.TaskStatus;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;

public class InMemoryTaskRepository : ITaskRepository
{
    private readonly ConcurrentDictionary<Guid, EntityTask> _tasks = new();
    private readonly ILogger<InMemoryTaskRepository> _logger;

    public InMemoryTaskRepository(ILogger<InMemoryTaskRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<EntityTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _tasks.TryGetValue(id, out var task);
        return Task.FromResult(task);
    }

    public Task<IEnumerable<EntityTask>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_tasks.Values.AsEnumerable());
    }

    public System.Threading.Tasks.Task AddAsync(EntityTask aggregate, CancellationToken cancellationToken = default)
    {
        if (!_tasks.TryAdd(aggregate.Id, aggregate))
        {
            throw new InvalidOperationException($"Task with ID {aggregate.Id} already exists.");
        }
        
        _logger.LogInformation("Task added: {TaskId}", aggregate.Id);
        return Task.CompletedTask;
    }

    public System.Threading.Tasks.Task UpdateAsync(EntityTask aggregate, CancellationToken cancellationToken = default)
    {
        _tasks.AddOrUpdate(aggregate.Id, aggregate, (key, oldValue) => aggregate);
        _logger.LogInformation("Task updated: {TaskId}", aggregate.Id);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _tasks.TryRemove(id, out _);
        _logger.LogInformation("Task deleted: {TaskId}", id);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<EntityTask>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        var tasks = _tasks.Values.Where(t => t.ProcessId == processId);
        return Task.FromResult(tasks);
    }

    public Task<EntityTask?> GetByElementIdAsync(Guid processId, string elementId, CancellationToken cancellationToken = default)
    {
        var task = _tasks.Values.FirstOrDefault(t => t.ProcessId == processId && t.ElementId == elementId);
        return Task.FromResult(task);
    }

    public Task<IEnumerable<EntityTask>> GetByStatusAsync(Guid processId, TaskStatus status, CancellationToken cancellationToken = default)
    {
        var tasks = _tasks.Values.Where(t => t.ProcessId == processId && t.Status == status);
        return Task.FromResult(tasks);
    }

    public Task<IEnumerable<EntityTask>> GetByAssigneeAsync(string assignee, CancellationToken cancellationToken = default)
    {
        var tasks = _tasks.Values.Where(t => t.AssignedTo == assignee);
        return Task.FromResult(tasks);
    }
}

