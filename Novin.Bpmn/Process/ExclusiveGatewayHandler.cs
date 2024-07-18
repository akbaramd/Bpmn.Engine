namespace Novin.Bpmn.Test.Process;


public class ExclusiveGatewayHandler : IGatewayHandler
{
    public async Task HandleGateway(ProcessNode node, ProcessEngine engine)
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
                if (await engine.scriptHandler.EvaluateConditionAsync(expression, globals))
                {
                    var newNode = engine.ConvertElementToNode(engine.definitionsHandler.GetElementById(flow.targetRef), node.Token);
                    await engine.StartProcess(newNode);
                    break;
                }
            }
        }
    }
}