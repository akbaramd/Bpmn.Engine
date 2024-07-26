namespace Novin.Bpmn.Abstractions;

public interface IBpmnTaskAccessor
{
    Task StoreTask(BpmnTask task);
    Task<BpmnTask?> RetrieveTask(Guid taskId);
    Task<BpmnTask?> RetrieveUserTask(string userId , Guid processId);

}