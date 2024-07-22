using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.Executors;

public class UserTaskExecutor : IExecutor
{
    public Task ExecuteAsync(BpmnNode node, BpmnEngine engine)
    {
        var element = engine.DefinitionsHandler.GetElementById(node.ElementId);
        if (element is BpmnUserTask userTask)
        {
            engine.State.WaitingUserTasks[node.ElementId] = node;
            node.CanBeContinue =
                false; // Console.WriteLine($"User task {userTask.id} is waiting for completion.");
        }

        return Task.CompletedTask;
    }
}