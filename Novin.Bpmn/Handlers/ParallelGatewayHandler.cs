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

            var currentInstance = node.Instances.Peek();
            currentInstance.Merges.Clear();

            var outgoingTasks = node.OutgoingFlows.Select(flow => CreateAndEnqueueNode(engine, node, flow, currentInstance.Tokens.First(), currentInstance.IsExecutable));
            await Task.WhenAll(outgoingTasks);

            currentInstance.IsExpired = true;
        }

        public bool CheckForParallelMerge(BpmnNode node)
        {
            var currentInstance = node.Instances.Peek();
            currentInstance.Merges.Push(new Tuple<string, bool>(currentInstance.Tokens.FirstOrDefault(),currentInstance.IsExecutable));

            return currentInstance.Merges.Count == node.IncomingFlows.Count;
        }

        private Task CreateAndEnqueueNode(BpmnEngine engine, BpmnNode node, BpmnSequenceFlow flow, string token, bool isExecutable)
        {
            var newNode = engine.CreateNewNode(engine.DefinitionsHandler.GetElementById(flow.targetRef), token, isExecutable, node.Instances.Peek().Tokens.First());

            // Add outgoing transition
            node.Instances.Peek().AddTransition(node.Instances.Peek().Tokens.First(), newNode.Instances.Peek().Tokens.First(), DateTime.Now, false,flow.id);

            engine.EnqueueNode(newNode);

            return Task.CompletedTask;
        }
    }
}