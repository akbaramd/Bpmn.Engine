namespace Novin.Bpmn.Engine.Domain.ValueObjects;

/// <summary>
/// نوع Boundary Event بر اساس event definition
/// </summary>
public enum BoundaryKind
{
    Timer,
    Message,
    Signal,
    Error,
    Escalation,
    Conditional,
    Cancel,
    Compensation
}
