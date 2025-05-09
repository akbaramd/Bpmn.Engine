using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Novin.Bpmn.Models;
using Novin.Bpmn.V3.Abstractions;

namespace Novin.Bpmn.V3.Handlers.Gateways
{
    /// <summary>
    /// Base implementation for gateway handlers with common functionality
    /// </summary>
    public abstract class BpmnV3BaseGatewayHandler : IBpmnV3GatewayHandler
    {
        /// <summary>
        /// Processes a gateway using dual token strategy
        /// </summary>
        public abstract Task<List<BpmnV3Token>> HandleGatewayAsync(BpmnGateway gateway, BpmnV3Token token, BpmnV3ProcessInstance processInstance);

        /// <summary>
        /// Base implementation to check if a gateway can be merged based on token count
        /// </summary>
        public virtual bool CanMerge(BpmnGateway gateway, BpmnV3ProcessInstance processInstance)
        {
            // Mark the current token as pending to merge
            var tokensAtGateway = processInstance.Tokens
                .Where(t => t.CurrentElementId == gateway.id && t.Status == TokenStatus.PendingToMerge)
                .ToList();

            // Get the number of incoming flows
            var incomingFlows = processInstance.DefinitionsHandler.GetIncomingSequenceFlows(gateway);
            
            // Default implementation: gateway can merge when all incoming paths have tokens
            return tokensAtGateway.Count >= incomingFlows.Count;
        }

        /// <summary>
        /// Create a new token for an outgoing flow
        /// </summary>
        protected BpmnV3Token CreateTokenForOutgoingFlow(BpmnSequenceFlow flow, BpmnV3Token sourceToken, bool isExecutable)
        {
            // Create a new token for the target element
            var newToken = new BpmnV3Token(flow.targetRef, flow.id)
            {
                ParentTokenId = sourceToken.Id
            };
            
            // Set executability based on parameter
            if (!isExecutable)
            {
                newToken.UnExecutable();
            }
            
            return newToken;
        }
        
        /// <summary>
        /// Completes all pending tokens at a gateway
        /// </summary>
        protected void CompletePendingTokens(List<BpmnV3Token> tokens)
        {
            foreach (var token in tokens)
            {
                token.Complete();
            }
        }
    }
} 