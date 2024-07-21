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

      
            var isExecutable = node.Merges.Any(x => x.Item2);
            var outgoingTasks = node.OutgoingTargets.Select(async flow =>
            {
                var globals = new ScriptGlobals { State = engine.State };
                if (!string.IsNullOrWhiteSpace(flow.conditionExpression?.Text.ToString()))
                {
                    var expression = string.Join(" ", flow.conditionExpression.Text);
                    if (!await engine.ScriptHandler.EvaluateConditionAsync(expression, globals))
                    {
                        CreateAndEnqueueNode(engine, node, flow, Guid.NewGuid().ToString(), false);
                        return;
                    }
                }
                CreateAndEnqueueNode(engine, node, flow, Guid.NewGuid().ToString(), isExecutable);
            });
            await Task.WhenAll(outgoingTasks);

            node.IsExpired = true;
        }

        public bool CheckForInclusiveMerge(BpmnNode node)
        {
          
            if (!node.Merges.Any())
                node.Merges = new Stack<Tuple<string,bool>>();

            node.Merges.Push(new Tuple<string, bool>(node.Tokens.First(),node.IsExecutable));
            return node.Merges.Count == node.IncommingFlows.Count;
        }

        private void CreateAndEnqueueNode(BpmnEngine engine, BpmnNode node, BpmnSequenceFlow flow, string token, bool isExecutable)
        {
            var element = engine.DefinitionsHandler.GetElementById(flow.targetRef);
            var newNode = engine.CreateNewNode(element, token, isExecutable, node.Tokens.First());
            engine.EnqueueNode(newNode);

            // Add outgoing transition
            node.AddTransition(node.Tokens.First(), token, DateTime.Now, false);
        }
    }
}
