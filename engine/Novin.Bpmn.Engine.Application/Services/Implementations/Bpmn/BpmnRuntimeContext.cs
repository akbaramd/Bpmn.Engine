namespace Novin.Bpmn.Engine.Application.Services;

public sealed record BpmnRuntimeContext(
    string BpmnProcessId,
    IBpmnModelAccessor Model
);