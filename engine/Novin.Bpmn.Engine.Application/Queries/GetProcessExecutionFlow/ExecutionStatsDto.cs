namespace Novin.Bpmn.Engine.Application.Queries.GetProcessExecutionFlow;

/// <summary>
/// Execution statistics
/// </summary>
public sealed class ExecutionStatsDto
{
    public int TotalTokens { get; init; }
    public int ExecutedElements { get; init; }
    public int ExecutedFlows { get; init; }
    public int BoundaryEventsConfigured { get; init; }
    public int BoundaryEventsTriggered { get; init; }
    public int ExecutionCycles { get; init; }
    public TimeSpan? TotalExecutionTime { get; init; }
}