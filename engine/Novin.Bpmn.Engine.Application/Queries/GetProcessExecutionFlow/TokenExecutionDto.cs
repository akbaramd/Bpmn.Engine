namespace Novin.Bpmn.Engine.Application.Queries.GetProcessExecutionFlow;

/// <summary>
/// Individual token execution through an element/flow
/// </summary>
public sealed class TokenExecutionDto
{
    public Guid TokenId { get; set; }
    public DateTime FirstExecutedAt { get; set; }
    public DateTime? LastExecutedAt { get; set; }
    public int ExecutionCount { get; set; }
}