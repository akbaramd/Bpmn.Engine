using Novin.Bpmn.Models;
using Novin.Bpmn.V2.Abstractions;

namespace Novin.Bpmn.V2.Handlers.Tasks;

public class Bpmn2TaskHandler : IBpmnV2TaskHandler
{
    private readonly IBpmnV2ScriptTaskHandler _scriptTaskHandler;

    public Bpmn2TaskHandler(IBpmnV2ScriptTaskHandler scriptTaskHandler)
    {
        _scriptTaskHandler = scriptTaskHandler ?? throw new ArgumentNullException(nameof(scriptTaskHandler));
    }

    public async Task HandleAsync(BpmnProcessNode node, BpmnProcessInstance instance,CancellationToken cancellationToken)
    {
        var element = instance.DefinitionsHandler.GetElementById(node.ElementId);

        switch (element)
        {
            case BpmnScriptTask:
                await ExecuteScriptTaskAsync(node, instance,cancellationToken);
                break;

            case BpmnUserTask:
                ExecuteUserTask(node);
                break;

            default:
                HandleUnsupportedTask(node);
                break;
        }
    }

    private async Task ExecuteScriptTaskAsync(BpmnProcessNode node, BpmnProcessInstance instance,CancellationToken cancellationToken)
    {
        await _scriptTaskHandler.HandleAsync(node, instance,cancellationToken);
        Console.WriteLine($"Executed script task: {node.ElementId}");
    }

    private void ExecuteUserTask(BpmnProcessNode node)
    {
        Console.WriteLine($"User task identified: {node.ElementId}");
        // User tasks require external intervention to mark completion
    }

    private void HandleUnsupportedTask(BpmnProcessNode node)
    {
        Console.WriteLine($"Unsupported task type for node: {node.ElementId}");
    }
}