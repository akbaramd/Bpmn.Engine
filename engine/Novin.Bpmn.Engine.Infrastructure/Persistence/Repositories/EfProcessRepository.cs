using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Task = System.Threading.Tasks.Task;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;

public class EfProcessRepository : IProcessRepository
{
    private readonly BpmnEngineDbContext _context;
    private readonly ILogger<EfProcessRepository> _logger;

    public EfProcessRepository(BpmnEngineDbContext context, ILogger<EfProcessRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Process?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Processes
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Process>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Processes
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Process aggregate, CancellationToken cancellationToken = default)
    {
        await _context.Processes.AddAsync(aggregate, cancellationToken);
        _logger.LogInformation("Process added: {ProcessId}", aggregate.Id);
    }

    public Task UpdateAsync(Process aggregate, CancellationToken cancellationToken = default)
    {
        _context.Processes.Update(aggregate);
        _logger.LogInformation("Process updated: {ProcessId}", aggregate.Id);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var process = await GetByIdAsync(id, cancellationToken);
        if (process != null)
        {
            _context.Processes.Remove(process);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Process deleted: {ProcessId}", id);
        }
    }

    public async Task<Process?> GetByDeploymentAndProcessBpmnIdAsync(Guid deploymentId, string processBpmnId, CancellationToken cancellationToken = default)
    {
        return await _context.Processes
            .FirstOrDefaultAsync(p => p.DeploymentId == deploymentId && p.ProcessBpmnId == processBpmnId, cancellationToken);
    }

    public async Task<IEnumerable<Process>> GetByStateAsync(ProcessState state, CancellationToken cancellationToken = default)
    {
        return await _context.Processes
            .Where(p => p.State == state)
            .ToListAsync(cancellationToken);
    }
}

