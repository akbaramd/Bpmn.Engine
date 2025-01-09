using Novin.Bpmn.V2.Abstractions;

namespace Novin.Bpmn.V2.Handlers.Gateways
{
    public class BpmnV2ParallelGatewayHandler : IBpmnV2GatewayHandler
    {
        public async Task<List<BpmnProcessNode>> HandleGatewayAsync(BpmnProcessNode processNode, BpmnProcessInstance instance)
        {
            var validPaths = new List<BpmnProcessNode>();

            try
            {
                // Check if the node can merge based on incoming flows
                if (!CheckForParallelMerge(processNode))
                {
                    Console.WriteLine($"Parallel Gateway {processNode.ElementId} is not ready to merge.");
                    return validPaths;
                }

                Console.WriteLine($"Parallel Gateway {processNode.ElementId} merging completed.");

                foreach (var flow in processNode.OutgoingFlows)
                {
                    try
                    {
                        // In Parallel Gateway, all outgoing paths are considered valid
                        var isExecutable = true;

                        // Create or retrieve the target node
                        var targetNode = instance.GetOrCreateNode(flow.targetRef, isExecutable, processNode, flow);
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
                Console.WriteLine($"Error handling parallel gateway {processNode.ElementId}: {ex.Message}");
                processNode.LogException(ex.Message);
                throw; // Re-throw to ensure upstream error handling
            }

            return validPaths;
        }

        public bool CheckForParallelMerge(BpmnProcessNode processNode)
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
                Console.WriteLine($"Error checking parallel merge for {processNode.ElementId}: {ex.Message}");
                processNode.LogException(ex.Message);
                throw;
            }
        }
    }
}
