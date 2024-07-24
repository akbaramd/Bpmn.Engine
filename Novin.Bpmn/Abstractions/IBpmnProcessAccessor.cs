namespace Novin.Bpmn.Abstractions;

public interface IBpmnProcessAccessor
{
    void StoreProcessState(string deploymentKey , Guid processId, BpmnProcessInstance? processState);
    BpmnProcessInstance? GetProcessState(Guid processId);
}