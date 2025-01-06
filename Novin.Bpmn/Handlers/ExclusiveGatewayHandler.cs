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
        public async Task HandleGateway(BpmnProcessNode processNode, BpmnProcessExecutor processExecutor)
        {
            try
            {
                // Check if the node can merge and proceed
                if (!CheckForExclusiveMerge(processNode))
                {
                    Console.WriteLine($"Exclusive Gateway {processNode.ElementId} is not ready to merge.");
                    return;
                }

                Console.WriteLine($"Exclusive Gateway {processNode.ElementId} merging completed.");

                foreach (var flow in processNode.OutgoingFlows)
                {
                    try
                    {
                        // Evaluate the condition for the flow
                        if (!await EvaluateFlowConditionAsync(flow, processExecutor))
                        {
                            Console.WriteLine($"Flow {flow.id} condition evaluated to false. Skipping.");
                            continue;
                        }

                        // Create and enqueue the next node if the condition is met
                        var newElement = processExecutor.DefinitionsHandler.GetElementById(flow.targetRef);
                        var newNode = processExecutor.CreateNewNode(newElement, Guid.NewGuid(), processNode.IsExecutable, processNode, flow);

                        processExecutor.EnqueueNext(newNode);
                        Console.WriteLine($"Flow {flow.id} condition met. Node {newNode.ElementId} enqueued.");
                        break; // Exit after finding the first matched condition (Exclusive Gateway logic).
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing flow {flow.id}: {ex.Message}");
                        processNode.LogException($"Flow {flow.id}: {ex.Message}");
                    }
                }

                // Expire the current node after processing
                processNode.Expire();
                Console.WriteLine($"Node {processNode.ElementId} expired after exclusive gateway processing.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling exclusive gateway {processNode.ElementId}: {ex.Message}");
                processNode.LogException(ex.Message);
                throw; // Re-throw to propagate error for upstream handling
            }
        }

        private bool CheckForExclusiveMerge(BpmnProcessNode processNode)
        {
            try
            {
                // Check if the node already has merges recorded
                if (processNode.Merges.Count > 0)
                {
                    Console.WriteLine($"Exclusive Gateway {processNode.ElementId} already merged.");
                    return true;
                }

                // Record the merge for the current node
                processNode.AddMerge(processNode.ElementId, processNode.Id, processNode.IsExecutable);
                Console.WriteLine($"Exclusive Gateway {processNode.ElementId} marked for merging.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking exclusive merge for {processNode.ElementId}: {ex.Message}");
                processNode.LogException(ex.Message);
                throw;
            }
        }

        private async Task<bool> EvaluateFlowConditionAsync(BpmnSequenceFlow flow, BpmnProcessExecutor processExecutor)
        {
            try
            {
                // Retrieve and validate the condition expression
                var expression = flow.conditionExpression?.Text?.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(expression))
                {
                    Console.WriteLine($"Flow {flow.id} has no valid condition expression. Skipping.");
                    return false;
                }

                var globals = new ScriptGlobals { State = processExecutor.Instance };

                // Evaluate the condition using the script handler
                var result = await processExecutor.ScriptHandler.EvaluateConditionAsync(expression, globals);
                Console.WriteLine($"Flow {flow.id} condition evaluated to {result}.");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error evaluating condition for flow {flow.id}: {ex.Message}");
                throw; // Re-throw to handle upstream
            }
        }
    }
}
