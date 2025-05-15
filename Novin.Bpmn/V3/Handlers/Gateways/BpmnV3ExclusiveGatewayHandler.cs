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
    /// Handles XOR/Exclusive Gateways with dual token strategy
    /// </summary>
    public class BpmnV3ExclusiveGatewayHandler : BpmnV3BaseGatewayHandler
    {
        private readonly ScriptHandler _scriptHandler;

        public BpmnV3ExclusiveGatewayHandler(ScriptHandler scriptHandler)
        {
            _scriptHandler = scriptHandler ?? throw new ArgumentNullException(nameof(scriptHandler));
        }

        /// <summary>
        /// Handles exclusive gateway according to BPMN 2.0 semantics with dual token strategy.
        /// For split: Only one path gets executable token, others get non-executable tokens
        /// For join: First arriving token wins, other tokens wait for merge
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

            // In exclusive gateway, we need at least one executable token to proceed with executable flow
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
            bool foundExecutablePath = false;
            
            // Process each outgoing flow
            foreach (var flow in outgoingFlows)
            {
                // For exclusive gateway, only the first valid path is executable
                bool isExecutable = anyExecutable && !foundExecutablePath;
                
                // If it has a condition, evaluate it
                if (!string.IsNullOrWhiteSpace(flow.conditionExpression?.Text.ToString()))
                {
                    var expression = string.Join(" ", flow.conditionExpression.Text);
                    var globals = new BpmnV3ScriptGlobals { Instance = processInstance };
                    bool conditionResult = await _scriptHandler.EvaluateConditionAsync(expression, globals);
                    
                    if (conditionResult && anyExecutable)
                    {
                        // This is an executable path
                        isExecutable = true;
                        foundExecutablePath = true;
                    }
                    else if (!conditionResult)
                    {
                        // Not a valid path at all, skip it
                        continue;
                    }
                }
                else if (isExecutable)
                {
                    // Default flow with no condition
                    foundExecutablePath = true;
                }
                
                // Create a token for this flow
                var newToken = CreateTokenForOutgoingFlow(flow, token, isExecutable);
                newTokens.Add(newToken);
                
                // For exclusive gateway, only one path should be executable
                if (foundExecutablePath)
                {
                    break;
                }
            }
            
            // If no path was found but we need to continue (default flow)
            if (newTokens.Count == 0 && anyExecutable)
            {
                // Find default flow if it exists
                var defaultFlow = outgoingFlows.FirstOrDefault(f => f.isImmediate || 
                    (gateway is BpmnExclusiveGateway eg && eg.@default == f.id));
                
                if (defaultFlow != null)
                {
                    var defaultToken = CreateTokenForOutgoingFlow(defaultFlow, token, true);
                    newTokens.Add(defaultToken);
                }
            }
            
            return newTokens;
        }
        
        /// <summary>
        /// Exclusive gateways merge on first token arrival
        /// </summary>
        public override bool CanMerge(BpmnGateway gateway, BpmnV3ProcessInstance processInstance)
        {
            // Exclusive gateway merges as soon as one token arrives
            var tokensAtGateway = processInstance.Tokens
                .Where(t => t.CurrentElementId == gateway.id && t.Status == TokenStatus.PendingToMerge)
                .ToList();
            
            return tokensAtGateway.Count > 0;
        }
    }
} 