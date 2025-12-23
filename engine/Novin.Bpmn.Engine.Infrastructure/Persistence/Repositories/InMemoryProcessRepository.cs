using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Task = System.Threading.Tasks.Task;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;

public class InMemoryProcessRepository : IProcessRepository
{
    private readonly ConcurrentDictionary<Guid, Process> _processes = new();
    private readonly ILogger<InMemoryProcessRepository> _logger;

    public InMemoryProcessRepository(ILogger<InMemoryProcessRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Process?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _processes.TryGetValue(id, out var process);
        return Task.FromResult(process);
    }

    public Task<IEnumerable<Process>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_processes.Values.AsEnumerable());
    }

    public Task AddAsync(Process aggregate, CancellationToken cancellationToken = default)
    {
        if (!_processes.TryAdd(aggregate.Id, aggregate))
        {
            throw new InvalidOperationException($"Process with ID {aggregate.Id} already exists.");
        }
        
        _logger.LogInformation("Process added: {ProcessId}", aggregate.Id);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Process aggregate, CancellationToken cancellationToken = default)
    {
        _processes.AddOrUpdate(aggregate.Id, aggregate, (key, oldValue) => aggregate);
        _logger.LogInformation("Process updated: {ProcessId}", aggregate.Id);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _processes.TryRemove(id, out _);
        _logger.LogInformation("Process deleted: {ProcessId}", id);
        return Task.CompletedTask;
    }

    public Task<Process?> GetByProcessDefinitionIdAsync(string processDefinitionId, CancellationToken cancellationToken = default)
    {
        var process = _processes.Values.FirstOrDefault(p => p.ProcessDefinitionId == processDefinitionId);
        return Task.FromResult(process);
    }

    public Task<IEnumerable<Process>> GetByStateAsync(ProcessState state, CancellationToken cancellationToken = default)
    {
        var processes = _processes.Values.Where(p => p.State == state);
        return Task.FromResult(processes);
    }
}

