using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Repository interface for Process aggregate
/// </summary>
public interface IProcessRepository : IRepository<Process>
{
    Task<Process?> GetByDeploymentAndProcessBpmnIdAsync(Guid deploymentId, string processBpmnId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Process>> GetByStateAsync(Domain.ValueObjects.ProcessState state, CancellationToken cancellationToken = default);
    Task UpdateAsync(Process process, CancellationToken cancellationToken = default);
}

