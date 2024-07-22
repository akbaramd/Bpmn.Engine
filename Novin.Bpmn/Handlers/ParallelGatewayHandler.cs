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

            var outgoingTasks = node.OutgoingFlows.Select(flow => CreateAndEnqueueNode(engine, node, flow, node.Id, node.IsExecutable));
            await Task.WhenAll(outgoingTasks);

            node.IsExpired = true;
        }

        public bool CheckForParallelMerge(BpmnNode node)
        {
            node.Merges.Push(new (node.ElementId,node.Id,node.IsExecutable));
            return node.Merges.Count == node.IncomingFlows.Count;
        }

        private Task CreateAndEnqueueNode(BpmnEngine engine, BpmnNode node, BpmnSequenceFlow flow, Guid id, bool isExecutable)
        {
            var newElement = engine.DefinitionsHandler.GetElementById(flow.targetRef);
            var newNode = engine.CreateNewNode(newElement, id, isExecutable, node,flow);

            // Add outgoing transition
            // node.AddTransition(node.Id, newNode.Id, DateTime.Now, false);

            engine.EnqueueNode(newNode);

            return Task.CompletedTask;
        }
    }
}