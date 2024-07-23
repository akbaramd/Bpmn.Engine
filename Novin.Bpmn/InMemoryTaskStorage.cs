using System.Collections.Concurrent;
using Novin.Bpmn.Abstractions;

namespace Novin.Bpmn;

public class InMemoryTaskStorage : ITaskStorage
{
    private readonly ConcurrentDictionary<string, BpmnTask> _tasks = new();

    public Task AddTaskAsync(BpmnTask task)
    {
        _tasks[task.TaskId] = task;
        return Task.CompletedTask;
    }

    public Task<BpmnTask?> GetTaskByIdAsync(string taskId)
    {
        _tasks.TryGetValue(taskId, out var task);
        return Task.FromResult(task);
    }

    public Task<IEnumerable<BpmnTask>> GetTasksForUserAsync(string userId)
    {
        var userTasks = _tasks.Values.Where(task => task.Assignee == userId || task.CandidateUsers.Contains(userId));
        return Task.FromResult(userTasks.AsEnumerable());
    }

    public Task<IEnumerable<BpmnTask>> GetTasksForGroupAsync(string group)
    {
        var groupTasks = _tasks.Values.Where(task => task.CandidateGroups.Contains(group));
        return Task.FromResult(groupTasks.AsEnumerable());
    }

    public Task ClaimTaskAsync(string taskId, string userId)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.Assignee = userId;
        }
        return Task.CompletedTask;
    }
}