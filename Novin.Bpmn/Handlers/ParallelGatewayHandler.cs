using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Novin.Bpmn.Handlers
{
    public class ParallelGatewayHandler : IGatewayHandler
    {
        public async Task HandleGateway(BpmnProcessNode processNode, BpmnProcessEngine processEngine)
        {
            if (!CheckForParallelMerge(processNode))
            {
                return;
            }

            var outgoingTasks = processNode.OutgoingFlows.Select(flow => CreateAndEnqueueNode(processEngine, processNode, flow, processNode.Id, processNode.IsExecutable));
            await Task.WhenAll(outgoingTasks);

            processNode.Expire();
        }

        public bool CheckForParallelMerge(BpmnProcessNode processNode)
        {
            processNode.AddMerge(processNode.ElementId, processNode.Id, processNode.IsExecutable);
            return processNode.Merges.Count == processNode.IncomingFlows.Count;
        }

        private Task CreateAndEnqueueNode(BpmnProcessEngine processEngine, BpmnProcessNode processNode, BpmnSequenceFlow flow, Guid id, bool isExecutable)
        {
            var newElement = processEngine.DefinitionsHandler.GetElementById(flow.targetRef);
            var newNode = processEngine.CreateNewNode(newElement, Guid.NewGuid(), isExecutable, processNode, flow);

            processEngine.EnqueueNode(newNode);

            return Task.CompletedTask;
        }
    }
}