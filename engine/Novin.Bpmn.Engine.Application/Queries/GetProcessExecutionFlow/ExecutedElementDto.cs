namespace Novin.Bpmn.Engine.Application.Queries.GetProcessExecutionFlow;

/// <summary>
/// BPMN element that was executed
/// </summary>
public sealed class ExecutedElementDto
{
    public string ElementId { get; init; } = default!;
    public string ElementType { get; init; } = default!;
    public string? ElementName { get; init; }

    public DateTime FirstExecutedAt { get; init; }

    // ✅ rename variable meaning: this is the calculated execution count
    public int CalculatedExecutionCount { get; init; }

    // ✅ single status for node (latest node instance status)
    public string Status { get; init; }

    public IReadOnlyCollection<TokenExecutionDto> TokenExecutions { get; init; } = Array.Empty<TokenExecutionDto>();
}