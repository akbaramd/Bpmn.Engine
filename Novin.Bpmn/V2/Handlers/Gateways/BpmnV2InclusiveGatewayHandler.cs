using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;

namespace Novin.Bpmn.V2.Handlers
{
    public class BpmnV2InclusiveGatewayHandler : IBpmnV2GatewayHandler
    {

        private readonly ScriptHandler scriptHandler;

        public BpmnV2InclusiveGatewayHandler(ScriptHandler scriptHandler)
        {
            this.scriptHandler = scriptHandler;
        }

        public async Task<List<BpmnProcessNode>> 
            HandleGatewayAsync(BpmnProcessNode processNode,BpmnProcessInstance instance)
        {
            var validPaths = new List<BpmnProcessNode>();

            try
            {
                // Check if the node can merge based on incoming flows
                if (!CheckForInclusiveMerge(processNode))
                {
                    Console.WriteLine($"Gateway {processNode.ElementId} is not ready to merge.");
                    return validPaths;
                }

                Console.WriteLine($"Inclusive Gateway {processNode.ElementId} merged successfully.");

                // Process outgoing flows and find valid paths
                foreach (var flow in processNode.OutgoingFlows)
                {
                    try
                    {
                        var isExecutable = processNode.IsExecutable;

                        // Evaluate condition if present
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


                        // Create a new process node for the target
                        var targetNode = instance.GetOrCreateNode(flow.targetRef, isExecutable, processNode, flow);
                        // Add the valid path to the list
                        validPaths.Add(targetNode);
                        Console.WriteLine($"Valid path found: Source {processNode.ElementId} -> Target {targetNode.ElementId}, Executable: {isExecutable}.");
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
                Console.WriteLine($"Error handling inclusive gateway {processNode.ElementId}: {ex.Message}");
                processNode.LogException(ex.Message);
                throw; // Re-throw to ensure upstream error handling
            }

            return validPaths;
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
    }
}
