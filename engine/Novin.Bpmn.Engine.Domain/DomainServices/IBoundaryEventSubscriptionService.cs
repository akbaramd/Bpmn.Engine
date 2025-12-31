using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services
{
    public interface IBoundaryEventSubscriptionService
    {
        Task<List<BoundaryEventSubscription>> SubscribeBoundaryEvents(NodeInstance node, CancellationToken ct);

        public class BoundaryEventSubscriptionService : IBoundaryEventSubscriptionService
        {
            private readonly IProcessRepository _processRepository;
            private readonly INodeInstanceRepository _nodeInstanceRepository;
            private readonly IDeploymentRepository _deploymentRepository;
            private readonly IBoundarySubscriptionRepository _boundaryEventSubscriptionRepository;
            private readonly IBpmnQuery _bpmnQuery;

            public BoundaryEventSubscriptionService(
                IProcessRepository processRepository,
                INodeInstanceRepository nodeInstanceRepository,
                IDeploymentRepository deploymentRepository,
                IBoundarySubscriptionRepository boundaryEventSubscriptionRepository,
                IBpmnQuery bpmnQuery)
            {
                _processRepository = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
                _nodeInstanceRepository = nodeInstanceRepository ??
                                          throw new ArgumentNullException(nameof(nodeInstanceRepository));
                _deploymentRepository =
                    deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
                _boundaryEventSubscriptionRepository = boundaryEventSubscriptionRepository ??
                                                       throw new ArgumentNullException(
                                                           nameof(boundaryEventSubscriptionRepository));
                _bpmnQuery = bpmnQuery ?? throw new ArgumentNullException(nameof(bpmnQuery));
            }

            /// <summary>
            /// Subscribe boundary events for specific nodes.
            /// This method finds the boundary events in the BPMN definitions,
            /// creates subscriptions for nodes that are connected to these events.
            /// </summary>
            public async Task<List<BoundaryEventSubscription>> SubscribeBoundaryEvents(NodeInstance node,
                CancellationToken ct)
            {
                // Step 1: Get the process and deployment
                var process = await _processRepository.GetByIdAsync(node.ProcessId);
                if (process == null)
                {
                    // Log warning if process is not found
                    return new List<BoundaryEventSubscription>();
                }

                var deployment = await _deploymentRepository.GetByIdAsync(process.DeploymentId);
                if (deployment == null)
                {
                    // Log warning if deployment is not found
                    return new List<BoundaryEventSubscription>();
                }

                // Step 2: Get BPMN definitions from deployment (via BPMN Query)
                var bpmnProcess = _bpmnQuery.GetProcessOrThrow(deployment, process.ProcessBpmnId);

                // Step 3: Fetch all elements (nodes) from the BPMN process
                var allElements = _bpmnQuery.GetAllElements(deployment, process.ProcessBpmnId);

                // Step 4: Find boundary events from BPMN elements
                var boundaryEvents =
                    _bpmnQuery.GetAllElementsOfType<BpmnBoundaryEvent>(deployment, process.ProcessBpmnId);

                var subscriptions = new List<BoundaryEventSubscription>();

                // Step 5: Loop through boundary events and subscribe them for the specific node
                foreach (var boundaryEvent in boundaryEvents)
                {
                    // Find the node (activity) this boundary event is attached to
                    var nodeId = boundaryEvent.attachedToRef.Name;

                    // Only subscribe for boundary events attached to the provided node (nodeId matches the node's element)
                    if (nodeId != node.ElementId)
                        continue; // Skip if the boundary event does not relate to the given node

                    var nodeElement = allElements.FirstOrDefault(el => ReadString(el, "id") == nodeId);

                    if (nodeElement == null)
                    {
                        // Log if the node element is not found
                        continue;
                    }

                    // Step 6: Register boundary event subscriptions for this node
                    var subscription = BoundaryEventSubscription.Create(
                        processId: process.Id,
                        tokenId: node.TokenId,
                        nodeInstanceId: node.Id,// Placeholder for the actual token Id which should be passed from context
                        hostElementId: node.ElementId, // Attach this to the current node
                        boundaryElementId: boundaryEvent.id,
                        activityInstanceId:node.ActivityInstanceId,
                        kind: BoundaryKind.Error, // Example: Error, Timer, etc.
                        isInterrupting: boundaryEvent.cancelActivity
                    );

                    // Add the created subscription to the list
                    subscriptions.Add(subscription);

                    // Step 7: Save the subscription to the repository
                    await _boundaryEventSubscriptionRepository.AddAsync(subscription, ct);
                }

                // Step 8: Return the list of created subscriptions
                return subscriptions;
            }

            private static string? ReadString(object obj, string prop)
            {
                if (obj == null) return null;
                var p = obj.GetType().GetProperty(prop,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.IgnoreCase);
                return p?.GetValue(obj) as string;
            }
        }
    }
}