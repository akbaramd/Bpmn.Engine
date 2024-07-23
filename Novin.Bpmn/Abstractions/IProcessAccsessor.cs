namespace Novin.Bpmn.Abstractions;

public interface IProcessAccsessor
{
    void StoreProcessState(string deploymentKey , string processId, BpmnProcessState? processState);
    BpmnProcessState? GetProcessState(string deploymentKey , string processId);
}