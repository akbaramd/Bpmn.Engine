using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.Handlers
{
    public class ParallelGatewayHandler : IGatewayHandler
    {
        public async Task HandleGateway(BpmnNode node, BpmnEngine engine)
        {
            if (!CheckForParallelMerge(node))
            {
                return;
            }


            node.Merges.Clear();

            var outgoingTasks = node.OutgoingTargets.Select(flow => CreateAndEnqueueNode(engine, node, flow, node.Tokens.First(), node.IsExecutable));
            await Task.WhenAll(outgoingTasks);

            node.IsExpired = true;
        }

        public bool CheckForParallelMerge(BpmnNode node)
        {
            node.Merges.Push(new Tuple<string, bool>(node.Tokens.First(),node.IsExecutable));

            return node.Merges.Count == node.IncommingFlows.Count;
        }

        private Task CreateAndEnqueueNode(BpmnEngine engine, BpmnNode node, BpmnSequenceFlow flow, string token, bool isExecutable)
        {
            var newElement = engine.DefinitionsHandler.GetElementById(flow.targetRef);
            var newNode = engine.CreateNewNode(newElement, token, isExecutable, node.Tokens.First());

            // Add outgoing transition
            node.AddTransition(node.Tokens.First(), newNode.Tokens.First(), DateTime.Now, false);

            engine.EnqueueNode(newNode);

            return Task.CompletedTask;
        }
    }
}