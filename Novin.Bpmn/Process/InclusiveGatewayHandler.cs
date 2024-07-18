namespace Novin.Bpmn.Test.Process;

public class InclusiveGatewayHandler : IGatewayHandler
{
    public async Task HandleGateway(ProcessNode node, ProcessEngine engine)
    {
        if (!engine.CheckForInclusiveMerge(node))
        {
            return;
        }

        engine.State.GatewayMergeState.Remove(node.Element.id);

        var outgoing = engine.definitionsHandler.GetOutgoingSequenceFlows(node.Element);
        var tasks = outgoing.Select(async flow =>
        {
            var globals = new ScriptGlobals { State = engine.State };
            if (flow.conditionExpression != null)
            {
                var expression = string.Join(" ", flow.conditionExpression.Text);
                if (!await engine.scriptHandler.EvaluateConditionAsync(expression, globals))
                {
                    var newNode = engine.ConvertElementToNode(engine.definitionsHandler.GetElementById(flow.targetRef), engine.FakeToken);
                    await engine.StartProcess(newNode);
                    return;
                }
            }

            var token = Guid.NewGuid().ToString();
            var nextNode = engine.ConvertElementToNode(engine.definitionsHandler.GetElementById(flow.targetRef), token);
            await engine.StartProcess(nextNode);
        });
        await Task.WhenAll(tasks);
    }
}