namespace Novin.Bpmn.Abstractions;

public interface IBpmnTaskAccessor
{
    Task StoreTask(BpmnTask task);
    Task<BpmnTask?> RetrieveTask(Guid taskId);

}