using Novin.Bpmn;
using Novin.Bpmn.Abstractions;

public class ProcessStateManager
{
    private readonly IBpmnProcessAccessor _processAccessor;

    public ProcessStateManager(IBpmnProcessAccessor processAccessor)
    {
        _processAccessor = processAccessor;
    }

    public void StoreState(BpmnProcessInstance instance)
    {
        _processAccessor.StoreProcessState(instance.DeploymentKey, instance.Id, instance);
    }

    public BpmnProcessInstance LoadState(Guid processId)
    {
        return _processAccessor.GetProcessState(processId);
    }
}