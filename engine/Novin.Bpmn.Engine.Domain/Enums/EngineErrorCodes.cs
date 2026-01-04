namespace Novin.Bpmn.Engine.Application;

public enum EngineErrorKind
{
    Technical,   // System/infra exceptions, timeouts, crashes
    Logical,     // Business/validation rules, preconditions
    BpmnError    // BPMN ErrorCode semantics (catchable by boundary error)
}