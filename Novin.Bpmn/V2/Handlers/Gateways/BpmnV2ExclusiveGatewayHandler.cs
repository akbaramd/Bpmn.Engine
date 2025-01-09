using Novin.Bpmn.Core;
using Novin.Bpmn.V2.Abstractions;

namespace Novin.Bpmn.V2.Handlers.Gateways
{
    public class BpmnV2ExclusiveGatewayHandler : IBpmnV2GatewayHandler
    {
        private readonly ScriptHandler scriptHandler;

        public BpmnV2ExclusiveGatewayHandler(ScriptHandler scriptHandler)
        {
            this.scriptHandler = scriptHandler;
        }

        public async Task<List<BpmnProcessNode>> HandleGatewayAsync(BpmnProcessNode processNode, BpmnProcessInstance instance)
        {
            var validPaths = new List<BpmnProcessNode>();

            try
            {
                // Check if the node can merge based on incoming flows
                if (!CheckForExclusiveMerge(processNode))
                {
                    Console.WriteLine($"Exclusive Gateway {processNode.ElementId} is not ready to merge.");
                    return validPaths;
                }

                Console.WriteLine($"Exclusive Gateway {processNode.ElementId} merging completed.");

                foreach (var flow in processNode.OutgoingFlows)
                {
                    try
                    {
                        var isExecutable = true;

                        // Evaluate condition for the flow
                        if (!string.IsNullOrWhiteSpace(flow.conditionExpression?.Text.ToString()))
                        {
                            var expression = string.Join(" ", flow.conditionExpression.Text);
                            var globals = new ScriptGlobals { State = instance };
                            var conditionResult = await scriptHandler.EvaluateConditionAsync(expression, globals);

                            if (!conditionResult)
                            {
                                Console.WriteLine($"Flow condition for {flow.id} evaluated to false. Marking as not executable.");
                                isExecutable = false;
                            }
                        }

                        // Create or retrieve the target node if the condition is met
                        if (isExecutable)
                        {
                            var targetNode = instance.GetOrCreateNode(flow.targetRef, isExecutable, processNode, flow);
                            validPaths.Add(targetNode);
                            Console.WriteLine($"Valid path found: Source {processNode.ElementId} -> Target {targetNode.ElementId}, Executable: {isExecutable}.");

                            // Exclusive Gateway logic: stop after the first valid path
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing flow {flow.id}: {ex.Message}");
                        processNode.LogException($"Flow {flow.id}: {ex.Message}");
                    }
                }

                // Mark the current node as expired after processing
                processNode.Expire();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling exclusive gateway {processNode.ElementId}: {ex.Message}");
                processNode.LogException(ex.Message);
                throw; // Re-throw to ensure upstream error handling
            }

            return validPaths;
        }

        public bool CheckForExclusiveMerge(BpmnProcessNode processNode)
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
                Console.WriteLine($"Error checking exclusive merge for {processNode.ElementId}: {ex.Message}");
                processNode.LogException(ex.Message);
                throw;
            }
        }
    }
}
