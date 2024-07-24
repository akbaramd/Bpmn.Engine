using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Novin.Bpmn.Handlers
{
    public class ExclusiveGatewayHandler : IGatewayHandler
    {
        public async Task HandleGateway(BpmnProcessNode processNode, BpmnProcessEngine processEngine)
        {
            if (!CheckForExclusiveMerge(processNode)) return;

            foreach (var flow in processNode.OutgoingFlows)
            {
                if (!await EvaluateFlowConditionAsync(flow, processEngine)) continue;

                var newElement = processEngine.DefinitionsHandler.GetElementById(flow.targetRef);
                var newNode = processEngine.CreateNewNode(newElement, Guid.NewGuid(), processNode.IsExecutable, processNode, flow);

                processEngine.EnqueueNode(newNode);
                break;
            }

            processNode.Expire();
        }

        private bool CheckForExclusiveMerge(BpmnProcessNode processNode)
        {
            if (processNode.Merges.Count > 0)
                return true;

            processNode.AddMerge(processNode.ElementId, processNode.Id, processNode.IsExecutable);
            return false;
        }

        private async Task<bool> EvaluateFlowConditionAsync(BpmnSequenceFlow flow, BpmnProcessEngine processEngine)
        {
            var expression = flow.conditionExpression?.Text?.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            var globals = new ScriptGlobals { State = processEngine.Instance };
            return await processEngine.ScriptHandler.EvaluateConditionAsync(expression, globals);
        }
    }
}