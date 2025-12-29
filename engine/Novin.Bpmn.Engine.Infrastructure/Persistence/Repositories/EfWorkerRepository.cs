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

    public async Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Jobs.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IEnumerable<Job?>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Jobs.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Job? aggregate, CancellationToken cancellationToken = default)
    {
        await _context.Jobs.AddAsync(aggregate, cancellationToken);
        _logger.LogInformation("Job added: {WorkerId}", aggregate.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var worker = await GetByIdAsync(id, cancellationToken);
        if (worker != null)
        {
            _context.Jobs.Remove(worker);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Job deleted: {WorkerId}", id);
        }
    }

    public async Task<IEnumerable<Job?>> GetByProcessIdAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        return await _context.Jobs
            .Where(w => w.ProcessId == processId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Job?>> GetByStatusAsync(JobStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Jobs
            .Where(w => w.Status == status)
            .ToListAsync(cancellationToken);
    }



   

    public async Task<Job?> GetByTokenIdAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        return await _context.Jobs
            .FirstOrDefaultAsync(w => w.TokenId == tokenId, cancellationToken);
    }

    public async Task UpdateAsync(Job? worker, CancellationToken cancellationToken = default)
    {
        _context.Jobs.Update(worker);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Job updated: {WorkerId}, Status: {Status}",
            worker.Id, worker.Status);
    }

    public   async Task<Job?> GetByTokenAndElementAsync(Guid tokenId, string elementId, CancellationToken ct)
    {
        return await _context.Jobs
            .FirstOrDefaultAsync(w => w.TokenId == tokenId && w.ElementId == elementId, ct);
    }
}