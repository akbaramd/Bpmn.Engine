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

            var outgoingTasks = node.OutgoingTargets.Select(flow => CreateAndEnqueueNode(engine, node, flow, node.Uid, node.IsExecutable));
            await Task.WhenAll(outgoingTasks);

            node.IsExpired = true;
        }

        public bool CheckForParallelMerge(BpmnNode node)
        {
            node.Merges.Push(new Tuple<string,Guid, bool>(node.Id,node.Uid,node.IsExecutable));

            return node.Merges.Count == node.IncommingFlows.Count;
        }

        private Task CreateAndEnqueueNode(BpmnEngine engine, BpmnNode node, BpmnSequenceFlow flow, Guid id, bool isExecutable)
        {
            var newElement = engine.DefinitionsHandler.GetElementById(flow.targetRef);
            var newNode = engine.CreateNewNode(newElement, id, isExecutable, node.Uid);

            // Add outgoing transition
            node.AddTransition(node.Uid, newNode.Uid, DateTime.Now, false);

            engine.EnqueueNode(newNode);

            return Task.CompletedTask;
        }
    }
}