using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Queries.GetProcessExecutionFlow;

/// <summary>
/// Boundary event information
/// </summary>
public sealed class BoundaryEventDto
{
    public Guid SubscriptionId { get; init; }
    public string AttachedToElementId { get; init; } = default!;
    public string BoundaryEventId { get; init; } = default!;
    public BoundaryKind Kind { get; init; }
    public bool IsInterrupting { get; init; }
    public SubscriptionState State { get; init; }
    public string? ErrorCode { get; init; }
    public Guid? TokenScopeId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? TriggeredAt { get; init; }
}