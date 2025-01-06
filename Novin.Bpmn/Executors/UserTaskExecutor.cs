using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Exceptions;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Handlers;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.Executors;

public class UserTaskExecutor(IBpmnTaskAccessor bpmnTaskAccessor) : IUserTaskExecutor
{
    public async Task ExecuteAsync(BpmnProcessNode processNode, BpmnProcessExecutor processExecutor)
    {
        if (processNode.UserTask is not null && processNode.UserTask.IsCompleted)
            return;

        var element = processExecutor.DefinitionsHandler.GetElementById(processNode.ElementId);

        if (element is BpmnUserTask userTask)
        {
            try
            {
                // Validate the user task assignment
                ValidateUserTaskAssignment(userTask, processNode);

                // Create and assign the user task
                var customTask = CreateUserTask(userTask, processNode, processExecutor.Instance);
                processNode.AddUserTask(customTask);
                processExecutor.EnqueuePending(processNode);
                processExecutor.StoreProcessState();

                // Persist the user task
                await bpmnTaskAccessor.StoreTask(customTask);
            }
            catch (BpmnValidationException ex)
            {
                processNode.LogException($"Validation error for user task {processNode.Id}: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                var exceptionMessage = $"Error while creating or assigning user task {processNode.Id}: {ex.Message}";
                processNode.LogException(exceptionMessage);
                throw new InvalidOperationException(exceptionMessage, ex);
            }
        }
        else
        {
            var exceptionMessage = $"Process Node {processNode.Id} is not a valid user task.";
            processNode.LogException(exceptionMessage);
            throw new InvalidOperationException(exceptionMessage);
        }
    }

    private void ValidateUserTaskAssignment(BpmnUserTask userTask, BpmnProcessNode processNode)
    {
        if (string.IsNullOrEmpty(userTask.assignee) &&
            string.IsNullOrEmpty(userTask.candidateUsers) &&
            string.IsNullOrEmpty(userTask.candidateGroups))
        {
            throw new BpmnValidationException($"User Task {processNode.Id} must have an assignee, candidate users, or candidate groups.");
        }

    }

    private BpmnTask CreateUserTask(BpmnUserTask userTask, BpmnProcessNode processNode, BpmnProcessInstance instance)
    {
        var customTask = new BpmnTask(
            processNode.Id,
            userTask.formId,
            userTask.name,
            userTask.assignee,
            instance.Id,
            instance.DeploymentKey,
            isCompleted:false);

        if (!string.IsNullOrEmpty(userTask.candidateUsers))
        {
            var candidates = userTask.candidateUsers.Split(',', StringSplitOptions.RemoveEmptyEntries);
            customTask.AddCandidateUsers(candidates);
        }

        if (!string.IsNullOrEmpty(userTask.candidateGroups))
        {
            var candidateGroups = userTask.candidateGroups.Split(',', StringSplitOptions.RemoveEmptyEntries);
            customTask.AddCandidateGroups(candidateGroups);
        }

        return customTask;
    }
}
