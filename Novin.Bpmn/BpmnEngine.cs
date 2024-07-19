using Novin.Bpmn.Test.Abstractions;
using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Executors;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Handlers;
using Novin.Bpmn.Test.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Novin.Bpmn.Test;

public class BpmnEngine
{
    public BpmnDefinitionsHandler definitionsHandler { get; private set; }

    public ProcessState State { get; private set; }
    public readonly IExecutor ScriptExecuter;
    public readonly IExecutor UserTaskExecuter;
    public readonly ScriptHandler ScriptHandler;

    private readonly object pauseLock = new object();

    public BpmnEngine(string path, string? savedState = null)
    {
        var bpmnFileHandler = new BpmnFileDeserializer();
        ScriptHandler = new ScriptHandler();
        ScriptExecuter = new ScriptTaskExecutor();
        UserTaskExecuter = new UserTaskExecutor();
        var definition = bpmnFileHandler.Deserialize(path);
        definitionsHandler = new BpmnDefinitionsHandler(definition);

        if (savedState != null)
        {
            State = ProcessState.RestoreState(savedState, definition);
        }
        else
        {
            State = new ProcessState(definition);
        }
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

        BpmnNode? nodeToProcess = null;
        lock (State.ActiveNodes)
        {
            if (State.ActiveNodes.Any())
            {
                nodeToProcess = State.ActiveNodes.First();
            }
        }

        if (nodeToProcess != null)
        {
            await StartProcess(nodeToProcess, immediately);
        }
    }

    public async Task StartProcess(BpmnNode node, bool immediately = true)
    {
        if (State.IsStopped)
            return;

        await WaitIfPaused();

        try
        {
            if (node is not BpmnFakeNode)
            {
                switch (node.Element)
                {
                    case BpmnScriptTask scriptTask:
                        await ScriptExecuter.ExecuteAsync(scriptTask, this);
                        break;
                    case BpmnUserTask userTask:
                        await UserTaskExecuter.ExecuteAsync(userTask, this);
                        return; // Wait for user task completion
                    // Handle other task types here...
                }
            }

            lock (State.ActiveNodes)
            {
                State.ActiveNodes = new ConcurrentBag<BpmnNode>(State.ActiveNodes.Except(new[] { node }));
            }

            if (immediately)
            {
                await FindNextNodes(node);
            }
            else
            {
                var nextNodes = FindNextNodesSync(node);
                lock (State.ActiveNodes)
                {
                    foreach (var nextNode in nextNodes)
                    {
                        State.ActiveNodes.Add(nextNode);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Handle exception...
            Console.WriteLine($"Error processing node {node.Id}: {ex.Message}");
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
            }).ToList();

            await Task.WhenAll(tasks); // Wait for all tasks to start in parallel
        }
    }

    private List<BpmnNode> FindNextNodesSync(BpmnNode node)
    {
        var nextNodes = new List<BpmnNode>();

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
                handler.HandleGateway(node, this);
            }
        }
        else
        {
            var outgoing = definitionsHandler.GetOutgoingSequenceFlows(node.Element);
            foreach (var flow in outgoing)
            {
                var newNode = ConvertElementToNode(definitionsHandler.GetElementById(flow.targetRef), node.Token);
                nextNodes.Add(newNode);
            }
        }

        return nextNodes;
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

    private async Task WaitIfPaused()
    {
        await Task.Run(() =>
        {
            lock (pauseLock)
            {
                while (State.IsPaused)
                {
                    Monitor.Wait(pauseLock);
                }
            }
        });
    }

    public string SaveState()
    {
        return State.SaveState();
    }

    public async Task CompleteUserTask(string taskId)
    {
        if (State.WaitingUserTasks.TryRemove(taskId, out var node))
        {
            Console.WriteLine($"User task {node.Id} is completed.");
            await MoveToNextNodes(node); // Move to next nodes directly
        }
    }

    private async Task MoveToNextNodes(BpmnNode node)
    {
        var outgoing = definitionsHandler.GetOutgoingSequenceFlows(node.Element);
        var tasks = outgoing.Select(flow =>
        {
            var newNode = ConvertElementToNode(definitionsHandler.GetElementById(flow.targetRef), node.Token);
            State.ActiveNodes.Add(newNode);
            return StartProcess(newNode); // Create a task for each outgoing flow
        }).ToList();

        await Task.WhenAll(tasks); // Wait for all tasks to start in parallel
    }
}
