using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Novin.Bpmn;
using Novin.Bpmn.Core;
using Novin.Bpmn.Models;
using Novin.Bpmn.V3;

public class BpmnV3ProcessInstance
{
    // Process ID
    public string ProcessElementId { get; private set; }

    // BPMN XML definition
    public string DefinitionXml { get; private set; }

    public readonly Dictionary<Guid, List<BaseEvent>> TokenEvents = new();
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

    public void AddEventToToken(Guid tokenId, BaseEvent bpmnEvent)
    {
        if (!TokenEvents.ContainsKey(tokenId))
        {
            TokenEvents[tokenId] = new List<BaseEvent>();
        }
        TokenEvents[tokenId].Add(bpmnEvent);

        
        // Initialize the event
        bpmnEvent.Initialize();
    }
    
    public async Task TriggerEventsForToken(Guid tokenId)
    {
        if (TokenEvents.TryGetValue(tokenId, out var events))
        {
            foreach (var bpmnEvent in events)
            {
                await bpmnEvent.Trigger();
            }
        }
        else
        {
            Console.WriteLine($"No events found for Token {tokenId}");
        }
    }
    
    // Moves a token to the next element based on routing logic
    public async Task MoveToken(BpmnV3Token token,bool? isExecutable = null)
    {
        
        if (token.Status != TokenStatus.Active)
        {
            Console.WriteLine($"Token {token.Id} is not active and cannot be moved.");
            return;
        }

        var currentElement = DefinitionsHandler.GetElementById(token.CurrentElementId);

        // بررسی Boundary Event‌های متصل به نود
      

        // ادامه مدیریت توکن
        if (currentElement is BpmnGateway gateway)
        {
            await HandleGateway(token, gateway,isExecutable);
        }
        else
        {
            HandleNormalFlow(token, currentElement,isExecutable);
        }

        if (TokenEvents.TryGetValue(token.Id,out var list))
        {
            list.Clear();
        }
    }

    public async Task TriggerSpecificEvent<T>(Guid nodeId) where T : BaseEvent
    {
        if (TokenEvents.TryGetValue(nodeId, out var events))
        {
            foreach (var bpmnEvent in events.OfType<T>())
            {
                await bpmnEvent.Trigger();
            }
        }
        else
        {
            Console.WriteLine($"No events of type {typeof(T).Name} found for Node {nodeId}");
        }
    }
    private async Task HandleGateway(BpmnV3Token token, BpmnGateway gateway, bool? isExecutable)
    {
        switch (gateway)
        {
            case BpmnExclusiveGateway:
                HandleExclusiveGateway(token, gateway,isExecutable);
                break;
            case BpmnParallelGateway:
                HandleParallelGateway(token, gateway,isExecutable);
                break;
            case BpmnInclusiveGateway:
                await HandleInclusiveGateway(token, gateway,isExecutable);
                break;
            default:
                Console.WriteLine($"Unsupported gateway type: {gateway.GetType().Name}");
                break;
        }
    }

    private void HandleExclusiveGateway(BpmnV3Token token, BpmnGateway gateway, bool? isExecutable)
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
    private async Task<bool> HandleBoundaryEvent(BpmnBoundaryEvent boundaryEvent, BpmnV3Token token)
    {
        foreach (var eventDefinition in boundaryEvent.Items)
        {
            if (eventDefinition is BpmnErrorEventDefinition errorEventDefinition)
            {
                Console.WriteLine($"Handling error event on boundary of element {boundaryEvent.attachedToRef.Name}");

                // انتقال توکن به مسیر مرتبط با Error Event
                var outgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(boundaryEvent);
                if (outgoingFlows.Any())
                {
                    var flow = outgoingFlows.First(); // مسیر جدید
                    token.MoveTo(flow.targetRef, flow.id);
                    return true; // مدیریت Event کامل شد
                }
                else
                {
                    Console.WriteLine($"Error event on {boundaryEvent.id} has no outgoing flows.");
                }
            }
            else if (eventDefinition is BpmnTimerEventDefinition timerEventDefinition)
            {
                // مدیریت Timer Event
                Console.WriteLine($"Waiting for timer event {boundaryEvent.id}...");
                // await HandleTimerEvent(timerEventDefinition, boundaryEvent, token);
                return true;
            }
            else if (eventDefinition is BpmnSignalEventDefinition signalEventDefinition)
            {
                // مدیریت Signal Event
                Console.WriteLine($"Waiting for signal event {boundaryEvent.id}...");
                // await HandleSignalEvent(signalEventDefinition, boundaryEvent, token);
                return true;
            }
        }

        return false; // هیچ Boundary Event‌ای اجرا نشد
    }

    private void HandleParallelGateway(BpmnV3Token token, BpmnGateway gateway, bool? isExecutable)
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

    private async Task HandleInclusiveGateway(BpmnV3Token token, BpmnGateway gateway, bool? isExecutable)
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

    private void HandleNormalFlow(BpmnV3Token token, BpmnFlowElement element, bool? isExecutable)
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
                token.SetExecutable(isExecutable);
                token.MoveTo(flow.targetRef, flow.id);
            }
        }
    }
}