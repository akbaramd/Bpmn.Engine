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
    Forked,  // Parent token that has forked children
    Merged,   // Child token that has merged at join gateway
}

