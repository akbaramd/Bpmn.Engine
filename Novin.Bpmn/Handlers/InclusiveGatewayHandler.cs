using System.Collections;
using Novin.Bpmn.Test.Abstractions;
using Novin.Bpmn.Test.Core;

namespace Novin.Bpmn.Test.Handlers;

public class InclusiveGatewayHandler : IGatewayHandler
{
    public async Task HandleGateway(BpmnNode node, BpmnEngine engine)
    {
        if (!CheckForInclusiveMerge(engine,node))
        {
            return;
        }

        var useFakeToken = engine.State.GatewayMergeState[node.Id].All(x => x.Equals(BpmnFakeNode.FakeToken));
        engine.State.GatewayMergeState.Remove(node.Element.id);
        var outgoing = engine.definitionsHandler.GetOutgoingSequenceFlows(node.Element);
        var tasks = outgoing.Select(async flow =>
        {
            var globals = new ScriptGlobals { State = engine.State };
            if (flow.conditionExpression != null)
            {   
                var expression = string.Join(" ", flow.conditionExpression.Text);
                if (!await engine.ScriptHandler.EvaluateConditionAsync(expression, globals))
                {
                    var element = engine.definitionsHandler.GetElementById(flow.targetRef);
                    var node2 = new BpmnFakeNode(element);
                    engine.State.ActiveNodes.Add(node2);
                    await engine.StartProcess(node2);
                    return;
                }
            }

            
                            
            
            var token = Guid.NewGuid().ToString();
            var nextNode = engine.ConvertElementToNode(engine.definitionsHandler.GetElementById(flow.targetRef), token);
            var fakeNode = new BpmnFakeNode(engine.definitionsHandler.GetElementById(flow.targetRef));
            engine.State.ActiveNodes.Add(useFakeToken ? fakeNode : nextNode);
            await engine.StartProcess(useFakeToken ? fakeNode : nextNode);
        });
        await Task.WhenAll(tasks);
        
    }
    public bool CheckForInclusiveMerge(BpmnEngine engine,BpmnNode node)
    {
        if (!engine.State.GatewayMergeState.ContainsKey(node.Element.id))
            engine.State.GatewayMergeState[node.Element.id] = new Stack<string>();

        engine.State.GatewayMergeState[node.Element.id].Push(node.Token);

        var incomingFlows = engine.definitionsHandler.GetIncomingSequenceFlows(node.Element);
        return engine.State.GatewayMergeState[node.Element.id].Count == incomingFlows.Count;
    }
}