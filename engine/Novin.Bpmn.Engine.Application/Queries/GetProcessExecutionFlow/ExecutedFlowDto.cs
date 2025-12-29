namespace Novin.Bpmn.Engine.Application.Queries.GetProcessExecutionFlow;

/// <summary>
/// Sequence flow that was executed
/// </summary>
public sealed class ExecutedFlowDto
{
    public string FlowId { get; init; } = default!;
    public string SourceElementId { get; init; } = default!;
    public string TargetElementId { get; init; } = default!;
    public string? FlowName { get; init; }
    public string? ConditionExpression { get; init; } // For conditional flows
    public DateTime FirstExecutedAt { get; init; }
    public int ExecutionCount { get; init; }
    public IReadOnlyCollection<TokenExecutionDto> TokenExecutions { get; init; } = Array.Empty<TokenExecutionDto>();
}