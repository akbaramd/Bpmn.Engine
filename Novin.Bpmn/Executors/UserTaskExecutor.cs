using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Handlers;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.Executors;

public class UserTaskExecutor : IExecutor
{
    private readonly IBpmnTaskAccessor _bpmnTaskAccessor;
    private readonly IBpmnUserAccessor _bpmnUserAccessor;

    public UserTaskExecutor(IBpmnTaskAccessor bpmnTaskAccessor, IBpmnUserAccessor bpmnUserAccessor)
    {
        _bpmnTaskAccessor = bpmnTaskAccessor;
        _bpmnUserAccessor = bpmnUserAccessor;
    }

    public async Task ExecuteAsync(BpmnProcessNode processNode, BpmnProcessEngine processEngine)
    {
        if (processNode.UserTask is not null && processNode.UserTask.IsCompleted) return;
        
        var element = processEngine.DefinitionsHandler.GetElementById(processNode.ElementId);
        if (element is BpmnUserTask userTask)
        {
            var customTask = CreateUserTask(userTask, processNode, processEngine.Instance);
            processNode.AddUserTask(customTask);
            await _bpmnTaskAccessor.StoreTask(customTask);
        }
    }

    private BpmnTask CreateUserTask(BpmnUserTask userTask, BpmnProcessNode processNode, BpmnProcessInstance instance)
    {
        var customTask = new BpmnTask(
            processNode.Id.ToString(),
            userTask.name,
            userTask.assignee,
            instance.Id,
            instance.DeploymentKey);

        if (!string.IsNullOrEmpty(userTask.candidateUsers))
            customTask.AddCandidateUsers(userTask.candidateUsers.Split(','));

        if (!string.IsNullOrEmpty(userTask.candidateGroups))
            customTask.AddCandidateGroups(userTask.candidateGroups.Split(','));

        return customTask;
    }
}