namespace Novin.Bpmn.Engine.Application.Queries.GetProcessExecutionFlow;

/// <summary>
/// BPMN element that was executed (represents NodeInstance)
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
    public string Status { get; init; } = default!;

    // Node instance fields
    public Guid? NodeInstanceId { get; init; }
    public Guid? ScopeId { get; init; }
    public Guid? ActivityInstanceId { get; init; }
    public IReadOnlyList<string> ArrivedViaFlowIds { get; init; } = Array.Empty<string>();
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public string? ErrorMessage { get; init; }
    
    // Node variables
    public IReadOnlyDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>();

    public IReadOnlyCollection<TokenExecutionDto> TokenExecutions { get; init; } = Array.Empty<TokenExecutionDto>();
}