using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Novin.Bpmn.Models;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.V3.Handlers.Gateways
{
    /// <summary>
    /// Handles AND/Parallel Gateways with dual token strategy
    /// </summary>
    public class BpmnV3ParallelGatewayHandler : BpmnV3BaseGatewayHandler
    {
        /// <summary>
        /// Handles parallel gateway according to BPMN 2.0 semantics with dual token strategy.
        /// For split: All outgoing paths get tokens with same executability as incoming token
        /// For join: Waits for tokens from all incoming flows, then merges
        /// </summary>
        public override Task<List<BpmnV3Token>> HandleGatewayAsync(BpmnGateway gateway, BpmnV3Token token, BpmnV3ProcessInstance processInstance)
        {
            // Mark the incoming token as pending for merge
            token.SetPendingToMerge();
            
            // Check if we can merge now (enough tokens have arrived)
            if (!CanMerge(gateway, processInstance))
            {
                // Not ready to merge yet, no new tokens created
                return Task.FromResult(new List<BpmnV3Token>());
            }

            // Get all tokens at this gateway
            var tokensAtGateway = processInstance.Tokens
                .Where(t => t.CurrentElementId == gateway.id && t.Status == TokenStatus.PendingToMerge)
                .ToList();

            // In parallel gateway, any incoming non-executable token makes all outgoing non-executable
            bool allExecutable = tokensAtGateway.All(t => t.IsExecutable);
            
            // Mark all tokens as completed since we're processing the gateway
            CompletePendingTokens(tokensAtGateway);
            
            // Get outgoing flows
            var outgoingFlows = processInstance.DefinitionsHandler.GetOutgoingSequenceFlows(gateway);
            
            // If no outgoing flows, nothing more to do
            if (!outgoingFlows.Any())
            {
                return Task.FromResult(new List<BpmnV3Token>());
            }
            
            var newTokens = new List<BpmnV3Token>();
            
            // Process each outgoing flow - for parallel gateway, all paths are taken
            foreach (var flow in outgoingFlows)
            {
                // All outgoing tokens have the same executability based on all incoming tokens
                var newToken = CreateTokenForOutgoingFlow(flow, token, allExecutable);
                newTokens.Add(newToken);
            }
            
            return Task.FromResult(newTokens);
        }
        
        /// <summary>
        /// For parallel gateways, we need to wait for tokens from all incoming flows
        /// </summary>
        public override bool CanMerge(BpmnGateway gateway, BpmnV3ProcessInstance processInstance)
        {
            // Get incoming flows
            var incomingFlows = processInstance.DefinitionsHandler.GetIncomingSequenceFlows(gateway);
            
            // Get tokens waiting at this gateway
            var tokensAtGateway = processInstance.Tokens
                .Where(t => t.CurrentElementId == gateway.id && t.Status == TokenStatus.PendingToMerge)
                .ToList();
                
            // For parallel merge, we need tokens from ALL incoming paths
            return tokensAtGateway.Count == incomingFlows.Count;
        }
    }
} 