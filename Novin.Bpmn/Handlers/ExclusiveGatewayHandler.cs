using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;

namespace Novin.Bpmn.Handlers;

public class ExclusiveGatewayHandler : IGatewayHandler
{
    public async Task HandleGateway(BpmnProcessNode processNode, BpmnProcessEngine processEngine)
    {
        if (!CheckForExclusiveMerge(processNode)) return;

        foreach (var flow in processNode.OutgoingFlows)
        {
            var globals = new ScriptGlobals { State = processEngine.ProcessState };
            if (string.IsNullOrWhiteSpace(flow.conditionExpression.ToString())) continue;

            var expression = string.Join(" ", flow.conditionExpression.Text);
            if (!await processEngine.ScriptHandler.EvaluateConditionAsync(expression, globals)) continue;

            var newElement = processEngine.DefinitionsHandler.GetElementById(flow.targetRef);
            var newNode = processEngine.CreateNewNode(newElement, processNode.Id, processNode.IsExecutable, processNode, flow);

            // Add outgoing transition
            // node.AddTransition(node.Id, newNode.Id, DateTime.Now, false);
            processEngine.EnqueueNode(newNode);
            break;
        }

        processNode.IsExpired = true;
    }

    public bool CheckForExclusiveMerge(BpmnProcessNode processNode)
    {
        if (processNode.Merges.Count > 0)
            return true;

        processNode.Merges.Push(new (processNode.ElementId, processNode.Id, processNode.IsExecutable));
        return false;
    }
}