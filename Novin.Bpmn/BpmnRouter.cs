using System;
using System.Collections.Generic;
using System.Linq;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Models;
using Novin.Bpmn.Handlers;

namespace Novin.Bpmn
{
    public class BpmnRouter
    {
        private readonly BpmnDefinitionsHandler _definitionsHandler;

        public BpmnRouter(BpmnDefinitionsHandler definitionsHandler)
        {
            _definitionsHandler = definitionsHandler;
        }

        // Find the next nodes for a given process node
        public IEnumerable<BpmnProcessNode> FindNextNodes(BpmnProcessNode processNode, BpmnProcessInstance instance)
        {
            var outgoingFlows = processNode.OutgoingFlows;

            foreach (var flow in outgoingFlows)
            {
                var targetElement = _definitionsHandler.GetElementById(flow.targetRef);

                if (targetElement != null)
                {
                    var newNode = new BpmnProcessNode(
                        targetElement.id,
                        Guid.NewGuid(),
                        _definitionsHandler.GetIncomingSequenceFlows(targetElement),
                        _definitionsHandler.GetOutgoingSequenceFlows(targetElement)
                    );

                    yield return newNode;
                }
            }
        }

        // Handle routing for gateways
        public async Task HandleGatewayAsync(BpmnGateway gateway, BpmnProcessNode processNode,BpmnProcessExecutor executor)
        {
            IGatewayHandler? handler = gateway switch
            {
                BpmnInclusiveGateway _ => new InclusiveGatewayHandler(),
                BpmnExclusiveGateway _ => new ExclusiveGatewayHandler(),
                BpmnParallelGateway _ => new ParallelGatewayHandler(),
                _ => null
            };

            if (handler != null)
            {
               await  handler.HandleGateway(processNode, executor);
            }

        }
    }
}
