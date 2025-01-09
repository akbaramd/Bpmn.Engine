using Novin.Bpmn.Models;
using Novin.Bpmn.V2.Abstractions;
using ErrorEventHandler = Novin.Bpmn.V2.Handlers.Events.ErrorEventHandler;

namespace Novin.Bpmn.V2
{
    public class BpmnV2BoundaryEventHandler
    {
        // Dictionary to store registered events for each node
        private readonly Dictionary<Guid, List<(BpmnBoundaryEvent, IInboundEventHandler)>> _registeredEvents = new();

        // Dictionary to store boundary nodes for each process node
        private readonly Dictionary<Guid, List<BpmnProcessNode>> _boundaryNodes = new();

        /// <summary>
        /// Registers a boundary event handler for a specific node.
        /// </summary>
        public void RegisterEvent(Guid nodeId, BpmnBoundaryEvent boundaryEvent, IInboundEventHandler eventHandler)
        {
            if (!_registeredEvents.ContainsKey(nodeId))
            {
                _registeredEvents[nodeId] = new List<(BpmnBoundaryEvent, IInboundEventHandler)>();
            }

            _registeredEvents[nodeId].Add((boundaryEvent, eventHandler));
        }

        /// <summary>
        /// Registers all boundary events for all nodes in the process instance
        /// at process initialization/start and collects boundary nodes.
        /// </summary>
        public async Task RegisterAllBoundaryEventsForProcess(BpmnProcessInstance instance, CancellationToken cancellationToken)
        {
            foreach (var node in instance.NodeStack)
            {
                var boundaryNodes = await RegisterAttachedBoundaryEvents(node, instance, cancellationToken);

                if (!_boundaryNodes.ContainsKey(node.Id))
                {
                    _boundaryNodes[node.Id] = new List<BpmnProcessNode>();
                }

                _boundaryNodes[node.Id].AddRange(boundaryNodes); // Collect boundary nodes per process node
            }
        }

        /// <summary>
        /// Dynamically registers all attached boundary events for a single node
        /// from the definitions (e.g., ErrorEvent, TimerEvent, etc.).
        /// </summary>
        public async Task<List<BpmnProcessNode>> RegisterAttachedBoundaryEvents(
            BpmnProcessNode node,
            BpmnProcessInstance instance,
            CancellationToken cancellationToken)
        {
            var boundaryNodes = new List<BpmnProcessNode>();
            var boundaryEvents = instance.DefinitionsHandler.GetAttachedEvents(node.ElementId);

            if (boundaryEvents == null || !boundaryEvents.Any())
            {
                Console.WriteLine($"No boundary events found for node {node.ElementId}.");
                return boundaryNodes;
            }

            foreach (var boundaryEvent in boundaryEvents)
            {
                var isInterrupting = boundaryEvent.cancelActivity;

                if (boundaryEvent.Items.OfType<BpmnErrorEventDefinition>().Any())
                {
                    Console.WriteLine($"Registering ErrorEventHandler for Node: {node.ElementId}, IsInterrupting={isInterrupting}");
                    var errorHandler = new ErrorEventHandler(isInterrupting);

                    // Create a boundary event node
                    var boundaryNode = instance.GetOrCreateNode(boundaryEvent.id, false, node);
                    RegisterEvent(node.Id, boundaryEvent, errorHandler);
                    boundaryNodes.Add(boundaryNode);
                }
                else
                {
                    Console.WriteLine($"Unsupported boundary event type for Node: {node.ElementId}.");
                }
            }

            return boundaryNodes;
        }

        /// <summary>
        /// Invokes a particular boundary event for the given node manually.
        /// </summary>
        public async Task<BpmnProcessNode?> InvokeBoundaryEventAsync(
            Guid nodeId,
            BpmnProcessInstance instance,
            CancellationToken cancellationToken)
        {
            if (!_registeredEvents.ContainsKey(nodeId))
            {
                Console.WriteLine($"[InvokeBoundaryEventAsync] No events registered for NodeId {nodeId}.");
                return null;
            }

            var boundaryEventHandlers = _registeredEvents[nodeId];
            if (!boundaryEventHandlers.Any())
            {
                Console.WriteLine($"[InvokeBoundaryEventAsync] Node {nodeId} has no boundary event handlers.");
                return null;
            }

            var (boundaryEvent, eventHandler) = boundaryEventHandlers.First();
            var node = instance.NodeStack.FirstOrDefault(x => x.Id == nodeId);
            if (node == null)
            {
                Console.WriteLine($"[InvokeBoundaryEventAsync] Node {nodeId} not found in instance.");
                return null;
            }

            Console.WriteLine($"[InvokeBoundaryEventAsync] Invoking boundary event '{boundaryEvent.id}'.");

            BpmnProcessNode? boundaryNode = null;
            try
            {
                boundaryNode = await eventHandler.InvokeAsync(boundaryEvent, node, instance, cancellationToken);

                if (boundaryNode != null)
                {
                    boundaryNode.Activate(); // Activate the node
                    Console.WriteLine($"Boundary event node {boundaryNode.ElementId} is now active.");

                    if (!_boundaryNodes.ContainsKey(nodeId))
                    {
                        _boundaryNodes[nodeId] = new List<BpmnProcessNode>();
                    }

                    _boundaryNodes[nodeId].Add(boundaryNode); // Add to dictionary
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[InvokeBoundaryEventAsync] Error: {ex.Message}");
            }

            return boundaryNode;
        }

        /// <summary>
        /// Retrieves the boundary nodes for a specific process node.
        /// </summary>
        public List<BpmnProcessNode> GetBoundaryNodesForNode(Guid nodeId)
        {
            return _boundaryNodes.TryGetValue(nodeId, out var boundaryNodes) ? boundaryNodes : new List<BpmnProcessNode>();
        }

        /// <summary>
        /// Retrieves all boundary nodes for all process nodes.
        /// </summary>
        public Dictionary<Guid, List<BpmnProcessNode>> GetAllBoundaryNodes()
        {
            return _boundaryNodes;
        }
    }
}
