using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Repository interface for ProcessExecutionNode entities
/// </summary>
public interface IProcessExecutionNodeRepository : IRepository<ProcessExecutionNode>
{
    /// <summary>
    /// Update an existing execution node
    /// </summary>
    Task UpdateAsync(ProcessExecutionNode entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all execution nodes for a specific process
    /// </summary>
    Task<IEnumerable<ProcessExecutionNode>> GetByProcessIdAsync(
        Guid processId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get execution path from start to end for a process
    /// </summary>
    Task<IEnumerable<ProcessExecutionNode>> GetExecutionPathAsync(
        Guid processId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the last executed node for a process
    /// </summary>
    Task<ProcessExecutionNode?> GetLastExecutedNodeAsync(
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
    Task<ProcessExecutionNode?> GetNodeAsync(
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

/// <summary>
/// Execution statistics for a process
/// </summary>
public class ProcessExecutionStats
{
    public int TotalNodesExecuted { get; set; }
    public int CompletedNodes { get; set; }
    public DateTime? FirstExecutedAt { get; set; }
    public DateTime? LastExecutedAt { get; set; }
    public TimeSpan? TotalExecutionTime { get; set; }
    public Dictionary<string, int> NodeTypeCounts { get; set; } = new();
}