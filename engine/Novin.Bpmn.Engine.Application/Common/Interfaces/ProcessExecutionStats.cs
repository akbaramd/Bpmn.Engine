namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

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