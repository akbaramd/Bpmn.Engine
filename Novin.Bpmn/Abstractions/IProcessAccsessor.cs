namespace Novin.Bpmn.Abstractions;

public interface IProcessAccsessor
{
    void StoreProcessState(string processId, BpmnProcessState? processState);
    BpmnProcessState? GetProcessState(string processId);
}