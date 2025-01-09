namespace Novin.Bpmn.V2.Abstractions;

public interface IBpmnV2TaskHandler
{
    Task HandleAsync(BpmnProcessNode processNode, BpmnProcessInstance instance,CancellationToken cancellationToken);
}

public interface IBpmnV2ScriptTaskHandler : IBpmnV2TaskHandler
{
}