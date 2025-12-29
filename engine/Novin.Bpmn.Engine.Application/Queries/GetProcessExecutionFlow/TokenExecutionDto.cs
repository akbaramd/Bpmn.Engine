namespace Novin.Bpmn.Engine.Application.Queries.GetProcessExecutionFlow;

/// <summary>
/// Individual token execution through an element/flow
/// </summary>
public sealed class TokenExecutionDto
{
    public Guid TokenId { get; init; }
    public DateTime FirstExecutedAt { get; init; }
    public DateTime? LastExecutedAt { get; init; }
    public int ExecutionCount { get; init; }
}