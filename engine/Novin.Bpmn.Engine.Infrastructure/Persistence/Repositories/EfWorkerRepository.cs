using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;

public class EfWorkerRepository : IWorkerRepository
{
    private readonly BpmnEngineDbContext _context;
    private readonly ILogger<EfWorkerRepository> _logger;

    public EfWorkerRepository(BpmnEngineDbContext context, ILogger<EfWorkerRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Worker?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Workers.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IEnumerable<Worker>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Workers.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Worker aggregate, CancellationToken cancellationToken = default)
    {
        await _context.Workers.AddAsync(aggregate, cancellationToken);
        _logger.LogInformation("Worker added: {WorkerId}", aggregate.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var worker = await GetByIdAsync(id, cancellationToken);
        if (worker != null)
        {
            _context.Workers.Remove(worker);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Worker deleted: {WorkerId}", id);
        }
    }

    public async Task<IEnumerable<Worker>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        return await _context.Workers
            .Where(w => w.ProcessId == processId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Worker>> GetByStatusAsync(WorkerStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Workers
            .Where(w => w.Status == status)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Worker>> GetByTypeAsync(WorkerType type, CancellationToken cancellationToken = default)
    {
        return await _context.Workers
            .Where(w => w.Type == type)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Worker>> GetPendingWorkersAsync(string? assignee = null, string? clientId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Workers.Where(w => w.Status == WorkerStatus.Pending);

        if (!string.IsNullOrEmpty(assignee))
        {
            query = query.Where(w => w.Type == WorkerType.UserTask &&
                                    w.Metadata.ContainsKey("assignee") &&
                                    w.Metadata["assignee"].ToString() == assignee);
        }

        if (!string.IsNullOrEmpty(clientId))
        {
            query = query.Where(w => w.Type == WorkerType.ServiceTask &&
                                    w.Metadata.ContainsKey("targetClientId") &&
                                    (w.Metadata["targetClientId"].ToString() == clientId ||
                                     w.Metadata["targetClientId"] == null)); // Broadcast tasks
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<Worker?> GetByTokenIdAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        return await _context.Workers
            .FirstOrDefaultAsync(w => w.TokenId == tokenId, cancellationToken);
    }

    public async Task UpdateAsync(Worker worker, CancellationToken cancellationToken = default)
    {
        _context.Workers.Update(worker);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Worker updated: {WorkerId}, Status: {Status}",
            worker.Id, worker.Status);
    }
}