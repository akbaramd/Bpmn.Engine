using Novin.Bpmn.Test.Abstractions;
using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Executors;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Handlers;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test;

public class BpmnEngine
{
    public BpmnDefinitionsHandler definitionsHandler { get; private set; }

    public readonly ProcessState State;
    public readonly IExecutor ScriptExecuter;
    public readonly ScriptHandler ScriptHandler;

    private readonly object pauseLock = new object();

    public BpmnEngine(string path)
    {
        var bpmnFileHandler = new BpmnFileDeserializer();
        ScriptHandler = new ScriptHandler();
        ScriptExecuter = new ScriptTaskExecutor();
        var definition = bpmnFileHandler.Deserialize(path);
        definitionsHandler = new BpmnDefinitionsHandler(definition);
        State = new ProcessState(definition);
    }

    public BpmnNode ConvertElementToNode(BpmnFlowElement element, string? token = null)
    {
        token ??= Guid.NewGuid().ToString();
        return new BpmnNode
        {
            Id = element.id,
            Token = token,
            Element = element
        };
    }

    public async Task StartProcess(bool immediately = true)
    {
        
        

        if (!State.ActiveNodes.Any())
        {
            var startEvent = definitionsHandler.GetFirstStartEvent();
            var startNode = ConvertElementToNode(startEvent);
            State.ActiveNodes.Add(startNode);
        }
        
        foreach (var activeNode in State.ActiveNodes.ToList())
        {
            await StartProcess(activeNode, immediately);    
        }
        
    }

    public async Task StartProcess(BpmnNode node, bool immediately = true)
    {
        if (State.IsStopped)
            return;

        await WaitIfPaused();
        
        

        if (node is not BpmnFakeNode)
        {
            switch (node.Element)
            {
                case BpmnScriptTask scriptTask:

                    await ScriptExecuter.ExecuteAsync(scriptTask, this);
                    break;
            }
        }

        State.ActiveNodes.Remove(node);
        
        if (immediately)
        {
            await FindNextNodes(node);
        }
    }

    private async Task FindNextNodes(BpmnNode node)
    {
        if (node.Element is BpmnGateway gateway)
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
                await handler.HandleGateway(node, this);
            }
        }
        else
        {
            var outgoing = definitionsHandler.GetOutgoingSequenceFlows(node.Element);
            var tasks = outgoing.Select(flow =>
            {
                var newNode = ConvertElementToNode(definitionsHandler.GetElementById(flow.targetRef), node.Token);
                State.ActiveNodes.Add(newNode);
                return StartProcess(newNode); // Create a task for each outgoing flow
            });
            await Task.WhenAll(tasks); // Wait for all tasks to complete
        }
    }



    public bool CheckForExclusiveMerge(BpmnNode node)
    {
        if (!State.GatewayMergeState.ContainsKey(node.Element.id))
            State.GatewayMergeState[node.Element.id] = new Stack<string>();

        // If the token for this node is already recorded, it means another branch already reached the gateway
        if (State.GatewayMergeState[node.Element.id].Count > 0)
            return true;

        State.GatewayMergeState[node.Element.id].Push(node.Token);
        return false;
    }

    public bool CheckForParallelMerge(BpmnNode node)
    {
        if (!State.GatewayMergeState.ContainsKey(node.Element.id))
            State.GatewayMergeState[node.Element.id] = new Stack<string>();

        State.GatewayMergeState[node.Element.id].Push(node.Token);

        var incomingFlows = definitionsHandler.GetIncomingSequenceFlows(node.Element);
        return State.GatewayMergeState[node.Element.id].Count == incomingFlows.Count;
    }

    public void Pause()
    {
        lock (pauseLock)
        {
            State.IsPaused = true;
        }
    }

    public void Resume()
    {
        lock (pauseLock)
        {
            State.IsPaused = false;
            Monitor.PulseAll(pauseLock);
        }
    }

    public void Stop()
    {
        State.IsStopped = true;
    }

    private Task WaitIfPaused()
    {
        lock (pauseLock)
        {
            while (State.IsPaused)
            {
                Monitor.Wait(pauseLock);
            }
        }

        return Task.CompletedTask;
    }
}