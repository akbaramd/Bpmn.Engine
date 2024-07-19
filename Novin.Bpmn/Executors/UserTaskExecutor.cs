using Novin.Bpmn.Test.Models;
using System.Threading.Tasks;
using Novin.Bpmn.Test.Executors.Abstracts;

namespace Novin.Bpmn.Test.Executors;

public class UserTaskExecutor : IExecutor
{
    public Task ExecuteAsync(BpmnFlowElement element, BpmnEngine engine)
    {
        if (element is BpmnUserTask userTask)
        {
            var userTaskNode = engine.ConvertElementToNode(userTask);
            engine.State.WaitingUserTasks[userTaskNode.Id] = userTaskNode;
            Console.WriteLine($"User task {userTask.id} is waiting for completion.");
        }
        return Task.CompletedTask;
    }
}