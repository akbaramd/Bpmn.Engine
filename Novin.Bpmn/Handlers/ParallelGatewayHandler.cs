using Novin.Bpmn.Test.Abstractions;

namespace Novin.Bpmn.Test.Handlers;

public class ParallelGatewayHandler : IGatewayHandler
{
    public async Task HandleGateway(BpmnNode node, BpmnEngine engine)
    {
        if (!engine.CheckForParallelMerge(node))
        {
            return;
        }

        engine.State.GatewayMergeState.Remove(node.Element.id);

        var outgoing = engine.definitionsHandler.GetOutgoingSequenceFlows(node.Element);
        var tasks = outgoing.Select(flow =>
        {
            var newNode =
                engine.ConvertElementToNode(engine.definitionsHandler.GetElementById(flow.targetRef), node.Token);
            engine.State.ActiveNodes.Add(newNode);
            return engine.StartProcess(newNode);
        });
        await Task.WhenAll(tasks);
    }
}