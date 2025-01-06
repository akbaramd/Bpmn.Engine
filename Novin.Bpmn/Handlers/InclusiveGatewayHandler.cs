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
        public async Task HandleGateway(BpmnProcessNode processNode, BpmnProcessExecutor processExecutor)
        {
            try
            {
                // Check if the node can merge based on incoming flows
                if (!CheckForInclusiveMerge(processNode))
                {
                    Console.WriteLine($"Gateway {processNode.ElementId} is not ready to merge.");
                    return;
                }

                Console.WriteLine($"Inclusive Gateway {processNode.ElementId} merged successfully.");

                // Process outgoing flows
                var outgoingTasks = processNode.OutgoingFlows.Select(async flow =>
                {
                    try
                    {
                        var globals = new ScriptGlobals { State = processExecutor.Instance };

                        // Evaluate condition if present
                        if (!string.IsNullOrWhiteSpace(flow.conditionExpression?.Text.ToString()))
                        {
                            var expression = string.Join(" ", flow.conditionExpression.Text);
                            var conditionResult = await processExecutor.ScriptHandler.EvaluateConditionAsync(expression, globals);

                            if (!conditionResult)
                            {
                                Console.WriteLine($"Flow condition for {flow.id} evaluated to false. Skipping execution.");
                                CreateAndEnqueueNode(processExecutor, processNode, flow, Guid.NewGuid(), false);
                                return;
                            }
                        }

                        // Create and enqueue node for valid conditions or no condition
                        CreateAndEnqueueNode(processExecutor, processNode, flow, Guid.NewGuid(), processNode.IsExecutable);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing flow {flow.id}: {ex.Message}");
                        processNode.LogException($"Flow {flow.id}: {ex.Message}");
                    }
                });

                await Task.WhenAll(outgoingTasks);

                // Mark the current node as expired after processing all outgoing flows
                processNode.Expire();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling inclusive gateway {processNode.ElementId}: {ex.Message}");
                processNode.LogException(ex.Message);
                throw; // Re-throw to ensure upstream error handling
            }
        }

        public bool CheckForInclusiveMerge(BpmnProcessNode processNode)
        {
            try
            {
                // Record the merge for the current node
                processNode.AddMerge(processNode.ElementId, processNode.Id, processNode.IsExecutable);

                // A merge occurs when merges match the number of incoming flows
                return processNode.Merges.Count == processNode.IncomingFlows.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking inclusive merge for {processNode.ElementId}: {ex.Message}");
                processNode.LogException(ex.Message);
                throw;
            }
        }

        private void CreateAndEnqueueNode(BpmnProcessExecutor processExecutor, BpmnProcessNode processNode, BpmnSequenceFlow flow, Guid id, bool isExecutable)
        {
            try
            {
                // Retrieve the target element and create a new process node
                var newElement = processExecutor.DefinitionsHandler.GetElementById(flow.targetRef);
                var newNode = processExecutor.CreateNewNode(newElement, id, isExecutable, processNode, flow);

                // Enqueue the newly created node
                processExecutor.EnqueueNext(newNode);

                Console.WriteLine($"Created and enqueued node {newNode.ElementId} (Executable: {isExecutable}).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating and enqueuing node for flow {flow.id}: {ex.Message}");
                processNode.LogException($"Flow {flow.id}: {ex.Message}");
                throw;
            }
        }
    }
}
