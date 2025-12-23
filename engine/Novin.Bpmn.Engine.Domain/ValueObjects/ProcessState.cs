namespace Novin.Bpmn.Engine.Domain.ValueObjects;

/// <summary>
/// Value object representing the state of a process
/// </summary>
public enum ProcessState
{
    Created,
    Running,
    Suspended,
    Completed,
    Terminated,
    Failed
}

