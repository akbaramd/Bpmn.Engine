using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Novin.Bpmn;
using Novin.Bpmn.Core;
using Novin.Bpmn.Models;

public class BpmnV3ProcessInstance
{
    // Process ID
    public string ProcessElementId { get; private set; }

    // BPMN XML definition
    public string DefinitionXml { get; private set; }

    // List of tokens
    public List<BpmnV3Token> Tokens { get; private set; } = new List<BpmnV3Token>();

    [System.Text.Json.Serialization.JsonIgnore] // Exclude from JSON serialization to avoid redundancy
    [JsonIgnore] // Exclude from JSON serialization to avoid redundancy
    public BpmnDefinitions Definition => BpmnDefinitionSerializer.Deserialize(DefinitionXml);

    [System.Text.Json.Serialization.JsonIgnore] // Exclude from JSON serialization to avoid redundancy
    [JsonIgnore]
    public BpmnDefinitionsHandler DefinitionsHandler => new(Definition);

    // Constructor to initialize the process instance
    public BpmnV3ProcessInstance(string processElementId, string definitionXml)
    {
        ProcessElementId = processElementId;
        DefinitionXml = definitionXml;
    }

    public BpmnV3Token CreateUnExecutableToken(string startElementId, string? flowElementId = null)
    {
        var token = new BpmnV3Token(startElementId, flowElementId);
        token.UnExecutable();
        Tokens.Add(token);
        return token;
    }

    // Creates a new token
    public BpmnV3Token CreateToken(string startElementId, string? flowElementId = null)
    {
        var token = new BpmnV3Token(startElementId, flowElementId);
        Tokens.Add(token);
        return token;
    }

    // Moves a token to the next element based on routing logic
    public async Task MoveToken(BpmnV3Token token)
    {
        if (token.Status != TokenStatus.Active)
        {
            Console.WriteLine($"Token {token.Id} is not active and cannot be moved.");
            return;
        }

        var currentElement = DefinitionsHandler.GetElementById(token.CurrentElementId);

        if (currentElement is BpmnGateway gateway)
        {
            await HandleGateway(token, gateway);
        }
        else
        {
            HandleNormalFlow(token, currentElement);
        }
    }

    private async Task HandleGateway(BpmnV3Token token, BpmnGateway gateway)
    {
        switch (gateway)
        {
            case BpmnExclusiveGateway:
                HandleExclusiveGateway(token, gateway);
                break;
            case BpmnParallelGateway:
                HandleParallelGateway(token, gateway);
                break;
            case BpmnInclusiveGateway:
                await HandleInclusiveGateway(token, gateway);
                break;
            default:
                Console.WriteLine($"Unsupported gateway type: {gateway.GetType().Name}");
                break;
        }
    }

    private void HandleExclusiveGateway(BpmnV3Token token, BpmnGateway gateway)
    {
        var outgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(gateway);
        var selectedFlow = outgoingFlows.FirstOrDefault(flow =>
            DefinitionsHandler.EvaluateCondition(flow, token, this).GetAwaiter().GetResult());

        if (selectedFlow != null)
        {
            token.MoveTo(selectedFlow.targetRef, selectedFlow.id);
        }
        else
        {
            token.Expire();
        }
    }

    private void HandleParallelGateway(BpmnV3Token token, BpmnGateway gateway)
    {
        token.SetPendingToMerge();

        var incomingFlows = DefinitionsHandler.GetIncomingSequenceFlows(gateway);
        var tokensAtGateway = Tokens
            .Where(t => t.CurrentElementId == gateway.id && t.Status == TokenStatus.PendingToMerge).ToList();

        if (tokensAtGateway.Count == incomingFlows.Count)
        {
            foreach (var t in tokensAtGateway)
            {
                t.Complete();
            }

            var parentToken = token.ParentTokenId != null
                ? Tokens.FirstOrDefault(t => t.Id == token.ParentTokenId)
                : token;
            if (parentToken != null)
            {
                parentToken.Reactivate();

                var outgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(gateway);
                foreach (var flow in outgoingFlows)
                {
                    var newToken = CreateToken(flow.targetRef, flow.id);
                    newToken.ParentTokenId = parentToken.Id;
                }
            }
        }
        else
        {
            Console.WriteLine($"Waiting for more tokens to merge at parallel gateway {gateway.id}");
        }
    }

    private async Task HandleInclusiveGateway(BpmnV3Token token, BpmnGateway gateway)
    {
        token.SetPendingToMerge();

        var incomingFlows = DefinitionsHandler.GetIncomingSequenceFlows(gateway);
        var tokensAtGateway = Tokens
            .Where(t => t.CurrentElementId == gateway.id && t.Status == TokenStatus.PendingToMerge).ToList();

        if (tokensAtGateway.Count == incomingFlows.Count)
        {
            foreach (var t in tokensAtGateway)
            {
                t.Complete();
            }

            var parentToken = token.ParentTokenId != null
                ? Tokens.FirstOrDefault(t => t.Id == token.ParentTokenId)
                : token;

            if (parentToken != null)
            {
                var outgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(gateway);


                // If multiple outgoing flows, create new tokens
                foreach (var flow in outgoingFlows)
                {
                    if (await DefinitionsHandler.EvaluateCondition(flow, parentToken, this))
                    {
                        var newToken = CreateToken(flow.targetRef, flow.id);
                        newToken.ParentTokenId = parentToken.Id;
                    }
                    else
                    {
                        var inactiveToken = CreateUnExecutableToken(flow.targetRef, flow.id);
                        inactiveToken.ParentTokenId = parentToken.Id;
                    }
                }
            }
        }
        else
        {
        }
    }

    public List<BpmnV3Token> GetWaitingTokens()
    {
        return Tokens.Where(t => t.Status == TokenStatus.Waiting).ToList();
    }

    private void HandleNormalFlow(BpmnV3Token token, BpmnFlowElement element)
    {
        var outgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(element);

        if (!outgoingFlows.Any())
        {
            Console.WriteLine($"No outgoing flows for element {element.id}.");
            token.Complete();
            return;
        }

        if (element is BpmnUserTask)
        {
            token.SetWaiting(); // UnExecutable the parent token for user task
        }
        else
        {
            foreach (var flow in outgoingFlows)
            {
                token.MoveTo(flow.targetRef, flow.id);
            }
        }
    }
}