namespace Novin.Bpmn.Engine.Domain.ValueObjects;

/// <summary>
/// Value object representing the state of a token
/// </summary>
public enum TokenState
{
    Created,
    Active,
    Waiting,
    Completed,
    Terminated,
    Failed,
}

