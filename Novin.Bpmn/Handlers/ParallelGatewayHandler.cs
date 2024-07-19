// ParallelGatewayHandler.cs
using System.Linq;
using System.Threading.Tasks;
using Novin.Bpmn.Test.Abstractions;
using Novin.Bpmn.Test.Core;

namespace Novin.Bpmn.Test.Handlers
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

            var outgoingTasks = node.OutgoingFlows.Select(flow =>
            {
                var newNode = engine.CreateNewNode(engine.DefinitionsHandler.GetElementById(flow.targetRef),
                    currentInstance.Tokens.First(), currentInstance.IsExecutable);
                engine.State.ActiveNodes.Add(newNode);
                return engine.StartProcess(newNode);
            });
            await Task.WhenAll(outgoingTasks);

            currentInstance.IsExpired = true;
        }
          public bool CheckForParallelMerge(BpmnNode node)
        {
            var currentInstance = node.Instances.Peek();
            currentInstance.Merges.Push(currentInstance.Tokens.FirstOrDefault());

            return currentInstance.Merges.Count == node.IncomingFlows.Count;
        }

    }
    
}