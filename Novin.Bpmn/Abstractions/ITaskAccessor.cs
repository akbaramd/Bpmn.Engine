namespace Novin.Bpmn.Abstractions;

public interface ITaskStorage
{
    Task AddTaskAsync(BpmnTask task);
    Task<BpmnTask?> GetTaskByIdAsync(string taskId);
    Task<IEnumerable<BpmnTask>> GetTasksForUserAsync(string userId);
    Task<IEnumerable<BpmnTask>> GetTasksForGroupAsync(string group);
    Task ClaimTaskAsync(string taskId, string userId);
}