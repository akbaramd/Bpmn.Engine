using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test.Executors;

public class UserTaskExecutor : IUserTaskExecutor
{
    public void Execute(BpmnUserTask userTask, BpmnInstance instance)
    {
        Console.WriteLine($"User Task '{userTask.id}' requires user interaction. Waiting for user action.");
        instance.PendingUserTask = userTask; // Store the pending user task
    }

}