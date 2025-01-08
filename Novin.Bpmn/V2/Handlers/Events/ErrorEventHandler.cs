using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.Handlers
{
    /// <summary>
    /// Handles error boundary events in a BPMN process.
    /// </summary>
    public class ErrorEventHandler : IInboundEventHandler
    {
        public bool IsInterrupting { get; }

        public ErrorEventHandler(bool isInterrupting)
        {
            IsInterrupting = isInterrupting;
        }

        /// <summary>
        /// Called when the boundary event is first attached to the node.
        /// For an error event, you might do nothing here or subscribe to an error channel.
        /// </summary>
        public async Task HandleEventAsync(
            BpmnBoundaryEvent boundaryEvent,
            BpmnProcessNode node,
            BpmnProcessInstance instance,
            CancellationToken cancellationToken = default)
        {
            // For an error boundary, "setup" might be minimal or no-op.
            // For example, you could set some internal state or subscribe to an external error channel.
            Console.WriteLine($"[ErrorEventHandler] Setup (HandleEventAsync) called for node {node.ElementId} / BoundaryEvent {boundaryEvent.id}.");
            
            // In this example, do nothing asynchronous, so just return
            await Task.CompletedTask;
        }

        /// <summary>
        /// Called when an actual error occurs during the main node's execution (from a catch block).
        /// We create (or retrieve) the error boundary node and possibly mark the parent node as expired if interrupting.
        /// </summary>
        /// <param name="boundaryEvent">The BPMN boundary event definition (contains ID, cancelActivity, etc.)</param>
        /// <param name="node">The parent node where the error boundary is attached</param>
        /// <param name="instance">The BPMN process instance</param>
        /// <param name="cancellationToken">Cancellation token if needed</param>
        /// <returns>A newly created boundary flow node (or null if not applicable)</returns>
        public async Task<BpmnProcessNode?> InvokeAsync(
            BpmnBoundaryEvent boundaryEvent,
            BpmnProcessNode node, 
            BpmnProcessInstance instance, 
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"[ErrorEventHandler] InvokeAsync triggered for node {node.ElementId}, boundaryEvent: {boundaryEvent.id}.");

            // If this error event is interrupting => the parent node is effectively canceled
            if (IsInterrupting)
            {
                node.DeActivate();
                Console.WriteLine($"Node {node.ElementId} is interrupted by ErrorEventHandler for boundaryEvent {boundaryEvent.id}.");
            }

            // Create or retrieve a new node to represent the error boundary flow
            // Common practice is to create a node with an ID like "ErrorBoundary_{boundaryEvent.Id}" 
            // or something that references the boundary event specifically.
           

            var newNode = instance.GetOrCreateNode(
                elementId: boundaryEvent.id,
                IsInterrupting,
                node,
              null // or a specialized user task if needed
            );

            // Optionally do any extra logic. For example:
            // - Store the error details from the node's exception 
            // - newNode.LogException(...);

            await Task.CompletedTask; // If you need async logic, do it here

            return newNode;
        }
    }
}
