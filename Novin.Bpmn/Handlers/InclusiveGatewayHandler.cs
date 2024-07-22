using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Models;

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
                var globals = new ScriptGlobals { State = processEngine.ProcessState };
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

            processNode.IsExpired = true;
        }

        public bool CheckForInclusiveMerge(BpmnProcessNode processNode)
        {
            processNode.Merges.Push(new (processNode.ElementId, processNode.Id, processNode.IsExecutable));
            return processNode.Merges.Count == processNode.IncomingFlows.Count;
        }

        private void CreateAndEnqueueNode(BpmnProcessEngine processEngine, BpmnProcessNode processNode, BpmnSequenceFlow flow, Guid id, bool isExecutable)
        {
            var element = processEngine.DefinitionsHandler.GetElementById(flow.targetRef);
            var newNode = processEngine.CreateNewNode(element, id, isExecutable, processNode,flow);
            processEngine.EnqueueNode(newNode);

            // Add outgoing transition
            // node.AddTransition(node.Id, id, DateTime.Now, false);
        }
    }
}
