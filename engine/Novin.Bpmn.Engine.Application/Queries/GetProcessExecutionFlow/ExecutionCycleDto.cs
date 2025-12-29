namespace Novin.Bpmn.Engine.Application.Queries.GetProcessExecutionFlow;

/// <summary>
/// Execution cycle/scope information
/// </summary>
public sealed class ExecutionCycleDto
{
    public Guid ScopeId { get; init; }
    public string? ScopeName { get; init; } // Activity name that created the scope
    public Guid? ParentScopeId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int TokensInScope { get; init; }
    public int BoundaryEventsInScope { get; init; }
}