namespace Novin.Bpmn.Engine.Domain.ValueObjects;

/// <summary>
/// Value object representing the state of a BPMN node
/// </summary>
public enum NodeState
{
    Pending,
    Processing,
    Completed,
    Failed,
    Paused,
    Terminated
}

