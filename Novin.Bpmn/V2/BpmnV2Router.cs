using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Models;
using Novin.Bpmn.Handlers;
using Novin.Bpmn.V2.Handlers;

namespace Novin.Bpmn
{
    public class BpmnV2Router
    {
        private readonly BpmnV2ExclusiveGatewayHandler exclusiveGatewayHandler;
        private readonly BpmnV2InclusiveGatewayHandler inclusiveGatewayHandler;
        private readonly BpmnV2ParallelGatewayHandler parallelGatewayHandler;

        public BpmnV2Router(
            BpmnV2ExclusiveGatewayHandler exclusiveGatewayHandler,
            BpmnV2InclusiveGatewayHandler inclusiveGatewayHandler,
            BpmnV2ParallelGatewayHandler parallelGatewayHandler)
        {
            this.exclusiveGatewayHandler = exclusiveGatewayHandler ?? throw new ArgumentNullException(nameof(exclusiveGatewayHandler));
            this.inclusiveGatewayHandler = inclusiveGatewayHandler ?? throw new ArgumentNullException(nameof(inclusiveGatewayHandler));
            this.parallelGatewayHandler = parallelGatewayHandler ?? throw new ArgumentNullException(nameof(parallelGatewayHandler));
        }

        /// <summary>
        /// Finds the next nodes for a given process node.
        /// </summary>
        /// <param name="processNode">The current process node.</param>
        /// <param name="instance">The BPMN process instance.</param>
        /// <returns>A list of the next process nodes.</returns>
        public async Task<List<BpmnProcessNode>> FindNextNodesAsync(BpmnProcessNode processNode, BpmnProcessInstance instance)
        {
            if (processNode == null) throw new ArgumentNullException(nameof(processNode));
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            var element = instance.DefinitionsHandler.GetElementById(processNode.ElementId);

            if (element is BpmnGateway )
            {
                return await HandleGatewayAsync(processNode, instance);
            }

            // Non-gateway elements: Create or fetch target nodes from outgoing flows.
            var nextNodes = new List<BpmnProcessNode>();
            foreach (var flow in processNode.OutgoingFlows)
            {
                var targetNode = instance.GetOrCreateNode(flow.targetRef, processNode.IsExecutable, processNode, flow);
                nextNodes.Add(targetNode);
            }

            return nextNodes;
        }

        /// <summary>
        /// Handles routing for gateways.
        /// </summary>
        /// <param name="processNode">The current process node.</param>
        /// <param name="instance">The BPMN process instance.</param>
        /// <returns>A list of the next process nodes after the gateway.</returns>
        public async Task<List<BpmnProcessNode>> HandleGatewayAsync(BpmnProcessNode processNode, BpmnProcessInstance instance)
        {
            if (processNode == null) throw new ArgumentNullException(nameof(processNode));
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            var element = instance.DefinitionsHandler.GetElementById(processNode.ElementId);
            IBpmnV2GatewayHandler? handler = element switch
            {
                BpmnInclusiveGateway _ => inclusiveGatewayHandler,
                BpmnExclusiveGateway _ => exclusiveGatewayHandler,
                BpmnParallelGateway _ => parallelGatewayHandler,
                _ => null
            };

            if (handler != null)
            {
                return await handler.HandleGatewayAsync(processNode, instance);
            }

            throw new InvalidOperationException($"No handler found for gateway: {processNode.ElementId}");
        }
    }
}
