namespace Novin.Bpmn.Engine.Domain.ValueObjects;

/// <summary>
/// Value object representing the status of a task
/// </summary>
public enum TaskStatus
{
    Created,
    Ready,
    Active,
    Completed,
    Failed,
    Terminated
}

