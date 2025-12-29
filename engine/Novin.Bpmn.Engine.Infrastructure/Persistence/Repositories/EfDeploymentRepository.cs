using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Task = System.Threading.Tasks.Task;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;

public class EfDeploymentRepository : IDeploymentRepository
{
    private readonly BpmnEngineDbContext _context;
    private readonly ILogger<EfDeploymentRepository> _logger;

    public EfDeploymentRepository(BpmnEngineDbContext context, ILogger<EfDeploymentRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Deployment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Deployments.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IEnumerable<Deployment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Deployments.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Deployment aggregate, CancellationToken cancellationToken = default)
    {
        await _context.Deployments.AddAsync(aggregate, cancellationToken);
        _logger.LogInformation("Deployment added: {DeploymentId}", aggregate.Id);
    }

    public Task UpdateAsync(Deployment aggregate, CancellationToken cancellationToken = default)
    {
        _context.Deployments.Update(aggregate);
        _logger.LogInformation("Deployment updated: {DeploymentId}", aggregate.Id);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deployment = await GetByIdAsync(id, cancellationToken);
        if (deployment != null)
        {
            _context.Deployments.Remove(deployment);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Deployment deleted: {DeploymentId}", id);
        }
    }

    public async Task<Deployment?> GetByDeploymentKeyAsync(string deploymentKey, CancellationToken cancellationToken = default)
    {
        return await _context.Deployments
            .FirstOrDefaultAsync(d => d.DeploymentKey == deploymentKey, cancellationToken);
    }

    public async Task<IEnumerable<Deployment>> GetActiveDeploymentsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Deployments
            .Where(d => d.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<Deployment?> GetLatestByDeploymentKeyAsync(string deploymentKey, CancellationToken cancellationToken = default)
    {
        return await _context.Deployments
            .Where(d => d.DeploymentKey == deploymentKey)
            .OrderByDescending(d => d.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<Deployment>> GetByDeploymentKeyAndVersionAsync(string deploymentKey, CancellationToken cancellationToken = default)
    {
        return await _context.Deployments
            .Where(d => d.DeploymentKey == deploymentKey)
            .OrderByDescending(d => d.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetNextVersionAsync(string deploymentKey, CancellationToken cancellationToken = default)
    {
        var latestVersion = await _context.Deployments
            .Where(d => d.DeploymentKey == deploymentKey)
            .OrderByDescending(d => d.Version)
            .Select(d => d.Version)
            .FirstOrDefaultAsync(cancellationToken);

        return latestVersion + 1;
    }
}

