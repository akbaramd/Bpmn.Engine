using System.Collections.Concurrent;
using Novin.Bpmn.Abstractions;

namespace Novin.Bpmn;

public class InMemoryBpmnTaskAccessor : IBpmnTaskAccessor
{
    private readonly ConcurrentDictionary<string, BpmnTask> _tasks = new();

    public Task StoreTask(BpmnTask task)
    {
        _tasks[task.TaskId] = task;
        return Task.CompletedTask;
    }

    public Task<BpmnTask?> RetrieveTask(string taskId)
    {
        _tasks.TryGetValue(taskId, out var task);
        return Task.FromResult(task);
    }

    
}