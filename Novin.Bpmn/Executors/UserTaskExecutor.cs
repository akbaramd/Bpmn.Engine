using Novin.Bpmn.Test.Models;
using System.Threading.Tasks;
using Novin.Bpmn.Test.Executors.Abstracts;

namespace Novin.Bpmn.Test.Executors;

public class UserTaskExecutor : IExecutor
{
    public Task ExecuteAsync(BpmnNode node, BpmnEngine engine)
    {
        var element = engine.DefinitionsHandler.GetElementById(node.Id);
        if (element is BpmnUserTask userTask)
        {
            engine.State.WaitingUserTasks[node.Id] = node;
            node.Instances.Peek().CanBeContinue =
                false; // Console.WriteLine($"User task {userTask.id} is waiting for completion.");
        }

        return Task.CompletedTask;
    }
}