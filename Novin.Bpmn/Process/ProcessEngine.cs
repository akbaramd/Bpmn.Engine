using System;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.Test;
using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Executors;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;
using Novin.Bpmn.Test.Process;

public class ProcessEngine
{
    public BpmnDefinitionsHandler definitionsHandler { get; private set; }
    public readonly string FakeToken = "FakeToken";
    public ProcessState State { get; private set; }
    public readonly IExecutor scriptExecuter;
    public readonly ScriptHandler scriptHandler;

    private readonly object pauseLock = new object();

    public ProcessEngine(string path)
    {
        var bpmnFileHandler = new BpmnFileDeserializer();
        scriptHandler = new ScriptHandler();
        scriptExecuter = new ScriptTaskExecutor();
        var definition = bpmnFileHandler.Deserialize(path);
        definitionsHandler = new BpmnDefinitionsHandler(definition);
        State = new ProcessState(definition);
    }

    public ProcessNode ConvertElementToNode(BpmnFlowElement element, string? token = null)
    {
        token ??= Guid.NewGuid().ToString();
        return new ProcessNode
        {
            Id = element.id,
            Token = token,
            Element = element
        };
    }

    public async Task StartProcess(bool immediately = true)
    {
        var startEvent = definitionsHandler.GetFirstStartEvent();
        var startNode = ConvertElementToNode(startEvent);
        await StartProcess(startNode,immediately);
    }

    public async Task StartProcess(ProcessNode node,bool immediately = true)
    {
        if (State.IsStopped)
            return;

        await WaitIfPaused();

        switch (node.Element)
        {
            case BpmnScriptTask scriptTask:
                if (!node.Token.Equals(FakeToken))
                    await scriptExecuter.ExecuteAsync(scriptTask, this);
                break;
        }

        if (immediately)
        {
            await FindNextNodes(node);
        }
    }

    private async Task FindNextNodes(ProcessNode node)
    {
        if (node.Element is BpmnGateway gateway)
        {
            IGatewayHandler handler = gateway switch
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
                return StartProcess(newNode); // Create a task for each outgoing flow
            });
            await Task.WhenAll(tasks); // Wait for all tasks to complete
        }
    }

    public bool CheckForInclusiveMerge(ProcessNode node)
    {
        if (!State.GatewayMergeState.ContainsKey(node.Element.id))
            State.GatewayMergeState[node.Element.id] = new HashSet<string>();

        State.GatewayMergeState[node.Element.id].Add(node.Token);

        var incomingFlows = definitionsHandler.GetIncomingSequenceFlows(node.Element);
        return State.GatewayMergeState[node.Element.id].Count == incomingFlows.Count;
    }

    public bool CheckForExclusiveMerge(ProcessNode node)
    {
        if (!State.GatewayMergeState.ContainsKey(node.Element.id))
            State.GatewayMergeState[node.Element.id] = new HashSet<string>();

        // If the token for this node is already recorded, it means another branch already reached the gateway
        if (State.GatewayMergeState[node.Element.id].Count > 0)
            return true;

        State.GatewayMergeState[node.Element.id].Add(node.Token);
        return false;
    }

    public bool CheckForParallelMerge(ProcessNode node)
    {
        if (!State.GatewayMergeState.ContainsKey(node.Element.id))
            State.GatewayMergeState[node.Element.id] = new HashSet<string>();

        State.GatewayMergeState[node.Element.id].Add(node.Token);

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

    private async Task WaitIfPaused()
    {
        lock (pauseLock)
        {
            while (State.IsPaused)
            {
                Monitor.Wait(pauseLock);
            }
        }
    }
}
