using Novin.Bpmn.Test.Process;

public class ParallelGatewayHandler : IGatewayHandler
{
    public async Task HandleGateway(ProcessNode node, ProcessEngine engine)
    {
        if (!engine.CheckForParallelMerge(node))
        {
            return;
        }

        engine.State.GatewayMergeState.Remove(node.Element.id);

        var outgoing = engine.definitionsHandler.GetOutgoingSequenceFlows(node.Element);
        var tasks = outgoing.Select(flow =>
        {
            var newNode = engine.ConvertElementToNode(engine.definitionsHandler.GetElementById(flow.targetRef), node.Token);
            return engine.StartProcess(newNode);
        });
        await Task.WhenAll(tasks);
    }
}