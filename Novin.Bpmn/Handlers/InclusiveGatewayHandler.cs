using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.Handlers
{
    public class InclusiveGatewayHandler : IGatewayHandler
    {
        public async Task HandleGateway(BpmnNode node, BpmnEngine engine)
        {
            if (!CheckForInclusiveMerge(node))
            {
                return;
            }
            
            Console.WriteLine($"{node.Id} merged");
      
            var outgoingTasks = node.OutgoingTargets.Select(async flow =>
            {
                var globals = new ScriptGlobals { State = engine.State };
                if (!string.IsNullOrWhiteSpace(flow.conditionExpression?.Text.ToString()))
                {
                    var expression = string.Join(" ", flow.conditionExpression.Text);
                    if (!await engine.ScriptHandler.EvaluateConditionAsync(expression, globals))
                    {
                        CreateAndEnqueueNode(engine, node, flow, Guid.NewGuid(), false);
                        return;
                    }
                }
                CreateAndEnqueueNode(engine, node, flow, Guid.NewGuid(), node.IsExecutable);
            });
            await Task.WhenAll(outgoingTasks);

            node.IsExpired = true;
        }

        public bool CheckForInclusiveMerge(BpmnNode node)
        {
          
            if (!node.Merges.Any())
                node.Merges = new Stack<Tuple<string,Guid,bool>>();

            node.Merges.Push(new Tuple<string,Guid, bool>(node.Id,node.Uid,node.IsExecutable));
            return node.Merges.Count == node.IncommingFlows.Count;
        }

        private void CreateAndEnqueueNode(BpmnEngine engine, BpmnNode node, BpmnSequenceFlow flow, Guid id, bool isExecutable)
        {
            var element = engine.DefinitionsHandler.GetElementById(flow.targetRef);
            var newNode = engine.CreateNewNode(element, id, isExecutable, node.Uid);
            engine.EnqueueNode(newNode);

            // Add outgoing transition
            node.AddTransition(node.Uid, id, DateTime.Now, false);
        }
    }
}
