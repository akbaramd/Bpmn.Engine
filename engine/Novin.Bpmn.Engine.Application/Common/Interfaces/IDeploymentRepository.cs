using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Repository interface for Deployment aggregate
/// </summary>
public interface IDeploymentRepository : IRepository<Deployment>
{
    Task<Deployment?> GetByDeploymentKeyAsync(string deploymentKey, CancellationToken cancellationToken = default);
    Task<Deployment?> GetLatestByDeploymentKeyAsync(string deploymentKey, CancellationToken cancellationToken = default);
    Task<IEnumerable<Deployment>> GetByDeploymentKeyAndVersionAsync(string deploymentKey, CancellationToken cancellationToken = default);
    Task<IEnumerable<Deployment>> GetActiveDeploymentsAsync(CancellationToken cancellationToken = default);
    Task<int> GetNextVersionAsync(string deploymentKey, CancellationToken cancellationToken = default);
    Task UpdateAsync(Deployment deployment, CancellationToken cancellationToken = default);
}

