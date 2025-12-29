namespace Novin.Bpmn.Engine.Application.Queries.GetProcessExecutionFlow;

/// <summary>
/// BPMN element that was executed
/// </summary>
public sealed class ExecutedElementDto
{
    public string ElementId { get; init; } = default!;
    public string ElementType { get; init; } = default!; // StartEvent, UserTask, EndEvent, etc.
    public string? ElementName { get; init; }
    public DateTime FirstExecutedAt { get; init; }
    public int ExecutionCount { get; init; } // How many times this element was executed
    public IReadOnlyCollection<TokenExecutionDto> TokenExecutions { get; init; } = Array.Empty<TokenExecutionDto>();
}