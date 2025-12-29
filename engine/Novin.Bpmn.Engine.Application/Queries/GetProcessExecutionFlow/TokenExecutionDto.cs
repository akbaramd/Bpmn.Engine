namespace Novin.Bpmn.Engine.Application.Queries.GetProcessExecutionFlow;

/// <summary>
/// Individual token execution through an element/flow
/// </summary>
public sealed class TokenExecutionDto
{
    public Guid TokenId { get; init; }
    public DateTime ExecutedAt { get; init; }
    public Guid? ScopeId { get; init; }
    public bool IsExecutable { get; init; }
    public string CurrentElementId { get; set; }
}