using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Task = System.Threading.Tasks.Task;
using TaskEntity = Novin.Bpmn.Engine.Domain.Entities.Task;
using TaskStatus = Novin.Bpmn.Engine.Domain.ValueObjects.TaskStatus;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;

public class EfTaskRepository : ITaskRepository
{
    private readonly BpmnEngineDbContext _context;
    private readonly ILogger<EfTaskRepository> _logger;

    public EfTaskRepository(BpmnEngineDbContext context, ILogger<EfTaskRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TaskEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tasks.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IEnumerable<TaskEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Tasks.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TaskEntity aggregate, CancellationToken cancellationToken = default)
    {
        await _context.Tasks.AddAsync(aggregate, cancellationToken);
        _logger.LogInformation("Task added: {TaskId}", aggregate.Id);
    }

    public Task UpdateAsync(TaskEntity aggregate, CancellationToken cancellationToken = default)
    {
        _context.Tasks.Update(aggregate);
        _logger.LogInformation("Task updated: {TaskId}", aggregate.Id);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await GetByIdAsync(id, cancellationToken);
        if (task != null)
        {
            _context.Tasks.Remove(task);
            _logger.LogInformation("Task deleted: {TaskId}", id);
        }
    }

    public async Task<IEnumerable<TaskEntity>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        return await _context.Tasks
            .Where(t => t.ProcessId == processId)
            .ToListAsync(cancellationToken);
    }

    public async Task<TaskEntity?> GetByElementIdAsync(Guid processId, string elementId, CancellationToken cancellationToken = default)
    {
        return await _context.Tasks
            .FirstOrDefaultAsync(t => t.ProcessId == processId && t.ElementId == elementId, cancellationToken);
    }

    public async Task<IEnumerable<TaskEntity>> GetByStatusAsync(Guid processId, TaskStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Tasks
            .Where(t => t.ProcessId == processId && t.Status == status)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<TaskEntity>> GetByAssigneeAsync(string assignee, CancellationToken cancellationToken = default)
    {
        return await _context.Tasks
            .Where(t => t.AssignedTo == assignee)
            .ToListAsync(cancellationToken);
    }
}

