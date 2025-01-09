using Novin.Bpmn.Models;

namespace Novin.Bpmn.V2.Abstractions
{
    /// <summary>
    /// Represents a boundary (or inbound) event that can be attached to a BPMN node.
    /// </summary>
    public interface IInboundEventHandler
    {
        /// <summary>
        /// Called to "set up" or configure the boundary event on this node.
        /// For example, might schedule a timer, subscribe to a message queue, or 
        /// otherwise prepare the event logic to eventually be triggered.
        /// </summary>
        Task HandleEventAsync(
            BpmnBoundaryEvent boundaryEvent,
            BpmnProcessNode node, 
            BpmnProcessInstance instance, 
            CancellationToken cancellationToken
        );

        /// <summary>
        /// Actually waits (blocks) for the event to be triggered or invoked.
        /// Once triggered, returns a new BpmnProcessNode (if applicable) to route to. 
        /// Returns null if no new node is needed, or if the event is canceled.
        /// 
        /// This method typically completes in one of the following ways:
        ///   1) The event triggers => return a new (or existing) node.
        ///   2) The token is canceled => throw TaskCanceledException (e.g. if main flow finished first).
        ///   3) Some error occurs => throw Exception, to be caught by the main flow logic.
        /// </summary>
        Task<BpmnProcessNode?> InvokeAsync(
            BpmnBoundaryEvent boundaryEvent,
            BpmnProcessNode node, 
            BpmnProcessInstance instance, 
            CancellationToken cancellationToken
        );

        /// <summary>
        /// Indicates if the event is interrupting the main flow when triggered. 
        /// If true, the parent node is marked non-executable as soon as this event triggers.
        /// </summary>
        bool IsInterrupting { get; }
    }
}