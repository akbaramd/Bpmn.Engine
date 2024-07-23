using Novin.Bpmn;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Models;
using BpmnTask = Novin.Bpmn.BpmnTask;

public class UserTaskExecutor : IExecutor
{
    private readonly ITaskStorage _taskStorage;
    private readonly IUserAccessor userAccessor;

    public UserTaskExecutor(ITaskStorage taskStorage, IUserAccessor userAccessor)
    {
        _taskStorage = taskStorage;
        this.userAccessor = userAccessor;
    }

    public async Task ExecuteAsync(BpmnProcessNode processNode, BpmnProcessEngine processEngine)
    {
        var element = processEngine.DefinitionsHandler.GetElementById(processNode.ElementId);
        if (element is BpmnUserTask userTask)
        {
            var customTask = new BpmnTask()
            {
                TaskId = processNode.Id.ToString(),
                Name = userTask.name,
                CandidateUsers = userTask.CandidateUsers?.Split(',').ToList(),
                CandidateGroups = userTask.CandidateGroups?.Split(',').ToList(),
                Assignee = userTask.Assignee,
                ProcessId = processEngine.ProcessState.Id,
                IsCompleted = false
            };

            processNode.AddUserTask(customTask);
            await _taskStorage.AddTaskAsync(customTask);
        }
    }
}