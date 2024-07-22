using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;

namespace Novin.Bpmn.Handlers;

public class ExclusiveGatewayHandler : IGatewayHandler
{
    public async Task HandleGateway(BpmnNode node, BpmnEngine engine)
    {
        if (!CheckForExclusiveMerge(node)) return;

        foreach (var flow in node.OutgoingFlows)
        {
            var globals = new ScriptGlobals { State = engine.State };
            if (string.IsNullOrWhiteSpace(flow.conditionExpression.ToString())) continue;

            var expression = string.Join(" ", flow.conditionExpression.Text);
            if (!await engine.ScriptHandler.EvaluateConditionAsync(expression, globals)) continue;

            var newElement = engine.DefinitionsHandler.GetElementById(flow.targetRef);
            var newNode = engine.CreateNewNode(newElement, node.Id, node.IsExecutable, node, flow);

            // Add outgoing transition
            // node.AddTransition(node.Id, newNode.Id, DateTime.Now, false);
            engine.EnqueueNode(newNode);
            break;
        }

        node.IsExpired = true;
    }

    public bool CheckForExclusiveMerge(BpmnNode node)
    {
        if (node.Merges.Count > 0)
            return true;

        node.Merges.Push(new (node.ElementId, node.Id, node.IsExecutable));
        return false;
    }
}