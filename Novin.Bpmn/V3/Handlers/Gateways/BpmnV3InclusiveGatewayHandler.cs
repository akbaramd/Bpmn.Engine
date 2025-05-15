using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Novin.Bpmn.Core;
using Novin.Bpmn.Models;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.V3.Handlers.Gateways
{
    /// <summary>
    /// Handles OR/Inclusive Gateways with dual token strategy
    /// </summary>
    public class BpmnV3InclusiveGatewayHandler : BpmnV3BaseGatewayHandler
    {
        private readonly ScriptHandler _scriptHandler;

        public BpmnV3InclusiveGatewayHandler(ScriptHandler scriptHandler)
        {
            _scriptHandler = scriptHandler ?? throw new ArgumentNullException(nameof(scriptHandler));
        }

        /// <summary>
        /// Handles inclusive gateway according to BPMN 2.0 semantics with dual token strategy.
        /// For split: All paths with true conditions get executable tokens, others get non-executable tokens
        /// For join: Waits for tokens from all incoming active flows, then merges
        /// </summary>
        public override async Task<List<BpmnV3Token>> HandleGatewayAsync(BpmnGateway gateway, BpmnV3Token token, BpmnV3ProcessInstance processInstance)
        {
            // Mark the incoming token as pending for merge
            token.SetPendingToMerge();
            
            // Check if we can merge now (enough tokens have arrived)
            if (!CanMerge(gateway, processInstance))
            {
                // Not ready to merge yet, no new tokens created
                return new List<BpmnV3Token>();
            }

            // Get all tokens at this gateway
            var tokensAtGateway = processInstance.Tokens
                .Where(t => t.CurrentElementId == gateway.id && t.Status == TokenStatus.PendingToMerge)
                .ToList();

            // In inclusive gateway, we need at least one executable token to proceed with executable flow
            bool anyExecutable = tokensAtGateway.Any(t => t.IsExecutable);
            
            // Mark all tokens as completed since we're processing the gateway
            CompletePendingTokens(tokensAtGateway);
            
            // Get outgoing flows
            var outgoingFlows = processInstance.DefinitionsHandler.GetOutgoingSequenceFlows(gateway);
            
            // If no outgoing flows, nothing more to do
            if (!outgoingFlows.Any())
            {
                return new List<BpmnV3Token>();
            }
            
            var newTokens = new List<BpmnV3Token>();
            bool atLeastOneConditionTrue = false;
            
            // Process each outgoing flow
            foreach (var flow in outgoingFlows)
            {
                bool conditionResult = true;
                bool isExecutable = anyExecutable; // By default, token is executable if incoming was
                
                // If it has a condition, evaluate it
                if (!string.IsNullOrWhiteSpace(flow.conditionExpression?.Text.ToString()))
                {
                    var expression = string.Join(" ", flow.conditionExpression.Text);
                    var globals = new BpmnV3ScriptGlobals { Instance = processInstance };
                    conditionResult = await _scriptHandler.EvaluateConditionAsync(expression, globals);
                    
                    // For inclusive gateway, token is executable if condition is true AND incoming token was executable
                    isExecutable = conditionResult && anyExecutable;
                    
                    if (conditionResult)
                    {
                        atLeastOneConditionTrue = true;
                    }
                }
                
                // Inclusive gateway sends tokens on ALL outgoing flows
                // but only flows with true conditions get executable tokens
                var newToken = CreateTokenForOutgoingFlow(flow, token, isExecutable);
                newTokens.Add(newToken);
            }
            
            // If no condition evaluated to true but we need to continue (default flow)
            if (!atLeastOneConditionTrue && anyExecutable)
            {
                // Find default flow if it exists
                var defaultFlow = outgoingFlows.FirstOrDefault(f => 
                    (gateway is BpmnInclusiveGateway ig && ig.@default == f.id));
                
                if (defaultFlow != null)
                {
                    // Remove any existing token for this flow and create a new executable one
                    newTokens.RemoveAll(t => t.CurrentElementId == defaultFlow.targetRef);
                    var defaultToken = CreateTokenForOutgoingFlow(defaultFlow, token, true);
                    newTokens.Add(defaultToken);
                }
            }
            
            return newTokens;
        }
        
        /// <summary>
        /// For inclusive gateways, we need to wait for tokens from all active incoming flows
        /// </summary>
        public override bool CanMerge(BpmnGateway gateway, BpmnV3ProcessInstance processInstance)
        {
            // Get incoming flows
            var incomingFlows = processInstance.DefinitionsHandler.GetIncomingSequenceFlows(gateway);
            
            // Get tokens waiting at this gateway
            var tokensAtGateway = processInstance.Tokens
                .Where(t => t.CurrentElementId == gateway.id && t.Status == TokenStatus.PendingToMerge)
                .ToList();
                
            // For inclusive merge, we need to consider only active paths
            // In dual token strategy, this means we need tokens from all incoming paths
            return tokensAtGateway.Count >= incomingFlows.Count;
        }
    }
} 