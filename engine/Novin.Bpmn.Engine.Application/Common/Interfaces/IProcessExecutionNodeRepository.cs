using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Repository interface for ExecutedNode entities
/// </summary>
public interface IProcessExecutionNodeRepository : IRepository<ExecutedNode>
{
    /// <summary>
    /// Update an existing execution node
    /// </summary>
    Task UpdateAsync(ExecutedNode entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all execution nodes for a specific process
    /// </summary>
    Task<IEnumerable<ExecutedNode>> GetByProcessIdAsync(
        Guid processId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get execution path from start to end for a process
    /// </summary>
    Task<IEnumerable<ExecutedNode>> GetExecutionPathAsync(
        Guid processId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the last executed node for a process
    /// </summary>
    Task<ExecutedNode?> GetLastExecutedNodeAsync(
        Guid processId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a specific node has already been executed for a process
    /// </summary>
    Task<bool> NodeExistsAsync(
        Guid processId,
        string nodeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific execution node for a process
    /// </summary>
    Task<ExecutedNode?> GetNodeAsync(
        Guid processId,
        string nodeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get execution statistics for a process
    /// </summary>
    Task<ProcessExecutionStats> GetExecutionStatsAsync(
        Guid processId,
        CancellationToken cancellationToken = default);
}