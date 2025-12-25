using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

/// <summary>
/// Repository interface for ProcessExecutionNode entities
/// </summary>
public interface IProcessExecutionNodeRepository : IRepository<ProcessExecutionNode>
{
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