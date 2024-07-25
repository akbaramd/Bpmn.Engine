using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Novin.Bpmn.Handlers
{
    public class InclusiveGatewayHandler : IGatewayHandler
    {
        public async Task HandleGateway(BpmnProcessNode processNode, BpmnProcessEngine processEngine)
        {
            if (!CheckForInclusiveMerge(processNode))
            {
                return;
            }

            Console.WriteLine($"{processNode.ElementId} merged");

            var outgoingTasks = processNode.OutgoingFlows.Select(async flow =>
            {
                var globals = new ScriptGlobals { State = processEngine.Instance };
                if (!string.IsNullOrWhiteSpace(flow.conditionExpression?.Text.ToString()))
                {
                    var expression = string.Join(" ", flow.conditionExpression.Text);
                    if (!await processEngine.ScriptHandler.EvaluateConditionAsync(expression, globals))
                    {
                        CreateAndEnqueueNode(processEngine, processNode, flow, Guid.NewGuid(), false);
                        return;
                    }
                }
                CreateAndEnqueueNode(processEngine, processNode, flow, Guid.NewGuid(), processNode.IsExecutable);
            });
            await Task.WhenAll(outgoingTasks);

            processNode.Expire();
        }

        public bool CheckForInclusiveMerge(BpmnProcessNode processNode)
        {
            processNode.AddMerge(processNode.ElementId, processNode.Id, processNode.IsExecutable);
            return processNode.Merges.Count == processNode.IncomingFlows.Count;
        }

        private void CreateAndEnqueueNode(BpmnProcessEngine processEngine, BpmnProcessNode processNode, BpmnSequenceFlow flow, Guid id, bool isExecutable)
        {
            var newElement = processEngine.DefinitionsHandler.GetElementById(flow.targetRef);
            var newNode = processEngine.CreateNewNode(newElement, id, isExecutable, processNode, flow);
            processEngine.EnqueueNext(newNode);
        }
    }
}
