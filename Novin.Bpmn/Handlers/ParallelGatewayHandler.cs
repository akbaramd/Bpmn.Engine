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
        public async Task HandleGateway(BpmnProcessNode processNode, BpmnProcessExecutor processExecutor)
        {
            try
            {
                // Check if the gateway can merge based on incoming flows
                if (!CheckForParallelMerge(processNode))
                {
                    Console.WriteLine($"Parallel Gateway {processNode.ElementId} is not ready to merge.");
                    return;
                }

                Console.WriteLine($"Parallel Gateway {processNode.ElementId} merging completed. Activating outgoing flows.");

                // Create and enqueue tasks for all outgoing flows
                var outgoingTasks = processNode.OutgoingFlows.Select(flow =>
                    CreateAndEnqueueNodeAsync(processExecutor, processNode, flow, Guid.NewGuid(), processNode.IsExecutable)
                );

                await Task.WhenAll(outgoingTasks);

                // Expire the current node after activating all outgoing flows
                processNode.Expire();
                Console.WriteLine($"Parallel Gateway {processNode.ElementId} has expired.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling Parallel Gateway {processNode.ElementId}: {ex.Message}");
                processNode.LogException(ex.Message);
                throw; // Re-throw to propagate error for upstream handling
            }
        }

        public bool CheckForParallelMerge(BpmnProcessNode processNode)
        {
            try
            {
                // Record the merge for the current node
                processNode.AddMerge(processNode.ElementId, processNode.Id, processNode.IsExecutable);

                // A merge occurs when merges match the number of incoming flows
                var isMergeComplete = processNode.Merges.Count == processNode.IncomingFlows.Count;

                Console.WriteLine($"Parallel Gateway {processNode.ElementId}: Merge status - {(isMergeComplete ? "Complete" : "Incomplete")}.");
                return isMergeComplete;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking parallel merge for {processNode.ElementId}: {ex.Message}");
                processNode.LogException(ex.Message);
                throw;
            }
        }

        private async Task CreateAndEnqueueNodeAsync(BpmnProcessExecutor processExecutor, BpmnProcessNode processNode, BpmnSequenceFlow flow, Guid id, bool isExecutable)
        {
            try
            {
                // Retrieve the target element and create a new process node
                var newElement = processExecutor.DefinitionsHandler.GetElementById(flow.targetRef);
                var newNode = processExecutor.CreateNewNode(newElement, id, isExecutable, processNode, flow);

                // Enqueue the newly created node
                processExecutor.EnqueueNext(newNode);

                Console.WriteLine($"Created and enqueued node {newNode.ElementId} (Executable: {isExecutable}).");
                await Task.CompletedTask; // Simulate async behavior for potential future expansion
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating and enqueuing node for flow {flow.id}: {ex.Message}");
                processNode.LogException($"Flow {flow.id}: {ex.Message}");
                throw; // Re-throw to ensure the error is handled appropriately
            }
        }
    }
}
