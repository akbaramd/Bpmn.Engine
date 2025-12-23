using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Task = System.Threading.Tasks.Task;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Repositories;

public class InMemoryDeploymentRepository : IDeploymentRepository
{
    private readonly ConcurrentDictionary<Guid, Deployment> _deployments = new();
    private readonly ILogger<InMemoryDeploymentRepository> _logger;

    public InMemoryDeploymentRepository(ILogger<InMemoryDeploymentRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Deployment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _deployments.TryGetValue(id, out var deployment);
        return Task.FromResult(deployment);
    }

    public Task<IEnumerable<Deployment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_deployments.Values.AsEnumerable());
    }

    public Task AddAsync(Deployment aggregate, CancellationToken cancellationToken = default)
    {
        if (!_deployments.TryAdd(aggregate.Id, aggregate))
        {
            throw new InvalidOperationException($"Deployment with ID {aggregate.Id} already exists.");
        }
        
        _logger.LogInformation("Deployment added: {DeploymentId} (Key: {DeploymentKey}, Version: {Version})", 
            aggregate.Id, aggregate.DeploymentKey, aggregate.Version);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Deployment aggregate, CancellationToken cancellationToken = default)
    {
        _deployments.AddOrUpdate(aggregate.Id, aggregate, (key, oldValue) => aggregate);
        _logger.LogInformation("Deployment updated: {DeploymentId} (Key: {DeploymentKey}, Version: {Version})", 
            aggregate.Id, aggregate.DeploymentKey, aggregate.Version);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _deployments.TryRemove(id, out _);
        _logger.LogInformation("Deployment deleted: {DeploymentId}", id);
        return Task.CompletedTask;
    }

    public Task<Deployment?> GetByDeploymentKeyAsync(string deploymentKey, CancellationToken cancellationToken = default)
    {
        var deployment = _deployments.Values.FirstOrDefault(d => d.DeploymentKey == deploymentKey);
        return Task.FromResult(deployment);
    }

    public Task<Deployment?> GetLatestByDeploymentKeyAsync(string deploymentKey, CancellationToken cancellationToken = default)
    {
        var deployment = _deployments.Values
            .Where(d => d.DeploymentKey == deploymentKey)
            .OrderByDescending(d => d.Version)
            .FirstOrDefault();
        
        return Task.FromResult(deployment);
    }

    public Task<IEnumerable<Deployment>> GetByDeploymentKeyAndVersionAsync(string deploymentKey, CancellationToken cancellationToken = default)
    {
        var deployments = _deployments.Values
            .Where(d => d.DeploymentKey == deploymentKey)
            .OrderByDescending(d => d.Version)
            .ToList();
        
        return Task.FromResult<IEnumerable<Deployment>>(deployments);
    }

    public Task<IEnumerable<Deployment>> GetActiveDeploymentsAsync(CancellationToken cancellationToken = default)
    {
        var deployments = _deployments.Values
            .Where(d => d.IsActive)
            .OrderByDescending(d => d.DeployedAt)
            .ToList();
        
        return Task.FromResult<IEnumerable<Deployment>>(deployments);
    }

    public Task<int> GetNextVersionAsync(string deploymentKey, CancellationToken cancellationToken = default)
    {
        var maxVersion = _deployments.Values
            .Where(d => d.DeploymentKey == deploymentKey)
            .Select(d => d.Version)
            .DefaultIfEmpty(0)
            .Max();
        
        return Task.FromResult(maxVersion + 1);
    }
}

