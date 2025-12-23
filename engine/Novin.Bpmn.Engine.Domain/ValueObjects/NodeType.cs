namespace Novin.Bpmn.Engine.Domain.ValueObjects;

/// <summary>
/// Value object representing the type of a BPMN node
/// </summary>
public enum NodeType
{
    StartEvent,
    EndEvent,
    Task,
    UserTask,
    ServiceTask,
    ScriptTask,
    ManualTask,
    Gateway,
    ExclusiveGateway,
    ParallelGateway,
    InclusiveGateway,
    EventBasedGateway,
    SubProcess,
    IntermediateEvent
}

