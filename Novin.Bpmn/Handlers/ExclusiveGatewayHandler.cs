using Novin.Bpmn.Test.Abstractions;
using Novin.Bpmn.Test.Core;

namespace Novin.Bpmn.Test.Handlers;

public class ExclusiveGatewayHandler : IGatewayHandler
{
    public async Task HandleGateway(BpmnNode node, BpmnEngine engine)
    {
        if (!engine.CheckForExclusiveMerge(node))
        {
            return;
        }

        engine.State.GatewayMergeState.Remove(node.Element.id);

        var outgoing = engine.definitionsHandler.GetOutgoingSequenceFlows(node.Element);
        foreach (var flow in outgoing)
        {
            var globals = new ScriptGlobals { State = engine.State };
            if (flow.conditionExpression != null)
            {
                var expression = string.Join(" ", flow.conditionExpression.Text);
                if (await engine.ScriptHandler.EvaluateConditionAsync(expression, globals))
                {
                    var newNode = engine.ConvertElementToNode(engine.definitionsHandler.GetElementById(flow.targetRef),
                        node.Token);
                    engine.State.ActiveNodes.Add(newNode);
                    await engine.StartProcess(newNode);
                    break;
                }
            }
        }
    }
}