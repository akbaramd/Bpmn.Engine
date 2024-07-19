using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.Test.Abstractions;
using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Executors;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Handlers;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test
{
    public class BpmnEngine
    {
        public BpmnDefinitionsHandler DefinitionsHandler { get; }
        public ProcessState State { get; }
        public IExecutor ScriptExecutor { get; }
        public IExecutor UserTaskExecutor { get; }
        public ScriptHandler ScriptHandler { get; }

        private readonly object _pauseLock = new object();

        public BpmnEngine(string path, string? savedState = null)
        {
            var bpmnFileHandler = new BpmnFileDeserializer();
            ScriptHandler = new ScriptHandler();
            ScriptExecutor = new ScriptTaskExecutor();
            UserTaskExecutor = new UserTaskExecutor();
            var definition = bpmnFileHandler.Deserialize(path);
            DefinitionsHandler = new BpmnDefinitionsHandler(definition);

            State = savedState != null 
                ? ProcessState.RestoreState(savedState, definition) 
                : new ProcessState(definition);
        }

        public BpmnNode CreateNewNode(BpmnFlowElement element, string token, bool isExecutable)
        {
            lock (State.Nodes)
            {
                if (!State.Nodes.TryGetValue(element.id, out var node))
                {
                    node = new BpmnNode
                    {
                        Id = element.id,
                        IncomingFlows = DefinitionsHandler.GetIncomingSequenceFlows(element).ToList(),
                        OutgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(element).ToList()
                    };
                    State.Nodes[element.id] = node;
                }

                lock (node.Instances)
                {
                    var currentInstance = node.Instances.FirstOrDefault(instance => !instance.IsExpired);
                    if (currentInstance != null && currentInstance.Tokens.Contains(token))
                    {
                        return node;
                    }

                    if (currentInstance == null || currentInstance.IsExpired)
                    {
                        var newInstance = new BpmnNodeInstance
                        {
                            Timestamp = DateTime.Now,
                            IsExecutable = isExecutable
                        };
                        newInstance.Tokens.Add(token);
                        node.Instances.Push(newInstance);
                    }
                    else
                    {
                        currentInstance.Tokens.Add(token);
                    }
                }

                return node;
            }
        }

        public async Task StartProcess(bool immediately = true)
        {
            if (!State.ActiveNodes.Any())
            {
                var startEvent = DefinitionsHandler.GetFirstStartEvent();
                var startNode = CreateNewNode(startEvent, Guid.NewGuid().ToString(), true);
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
                var currentInstance = node.Instances.Peek();
                currentInstance.Details = $"Executed at {DateTime.Now}";

                if (currentInstance.IsExecutable)
                {
                    await ExecuteTask(node);
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
                Console.WriteLine($"Error processing node {node.Id}: {ex.Message}");
            }
        }

        private async Task ExecuteTask(BpmnNode node)
        {
            var element = DefinitionsHandler.GetElementById(node.Id);
            switch (element)
            {
                case BpmnScriptTask _:
                    await ScriptExecutor.ExecuteAsync(node, this);
                    break;
                case BpmnUserTask _:
                    await UserTaskExecutor.ExecuteAsync(node, this);
                    return;
            }
        }

        private async Task FindNextNodes(BpmnNode node)
        {
            var element = DefinitionsHandler.GetElementById(node.Id);
            if (element is BpmnGateway gateway)
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
                var tasks = node.OutgoingFlows.Select(flow =>
                {
                    var newNode = CreateNewNode(DefinitionsHandler.GetElementById(flow.targetRef),
                        node.Instances.Peek().Tokens.FirstOrDefault() ?? Guid.NewGuid().ToString(), true);
                    State.ActiveNodes.Add(newNode);
                    node.Instances.Peek().IsExpired = true;
                    return StartProcess(newNode);
                }).ToList();

                await Task.WhenAll(tasks);
            }
        }

        private List<BpmnNode> FindNextNodesSync(BpmnNode node)
        {
            var nextNodes = new List<BpmnNode>();
            var element = DefinitionsHandler.GetElementById(node.Id);

            if (element is BpmnGateway gateway)
            {
                IGatewayHandler? handler = gateway switch
                {
                    BpmnInclusiveGateway _ => new InclusiveGatewayHandler(),
                    BpmnExclusiveGateway _ => new ExclusiveGatewayHandler(),
                    BpmnParallelGateway _ => new ParallelGatewayHandler(),
                    _ => null
                };

                handler?.HandleGateway(node, this);
            }
            else
            {
                foreach (var flow in node.OutgoingFlows)
                {
                    var newNode = CreateNewNode(DefinitionsHandler.GetElementById(flow.targetRef),
                        node.Instances.Peek().Tokens.FirstOrDefault() ?? Guid.NewGuid().ToString(), true);
                    nextNodes.Add(newNode);
                }
            }

            return nextNodes;
        }

        public void Pause()
        {
            lock (_pauseLock)
            {
                State.IsPaused = true;
            }
        }

        public void Resume()
        {
            lock (_pauseLock)
            {
                State.IsPaused = false;
                Monitor.PulseAll(_pauseLock);
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
                lock (_pauseLock)
                {
                    while (State.IsPaused)
                    {
                        Monitor.Wait(_pauseLock);
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
                await MoveToNextNodes(node);
            }
        }

        private async Task MoveToNextNodes(BpmnNode node)
        {
            var tasks = node.OutgoingFlows.Select(flow =>
            {
                var newNode = CreateNewNode(DefinitionsHandler.GetElementById(flow.targetRef),
                    node.Instances.Peek().Tokens.FirstOrDefault() ?? Guid.NewGuid().ToString(), true);
                State.ActiveNodes.Add(newNode);
                return StartProcess(newNode);
            }).ToList();

            await Task.WhenAll(tasks);
        }
    }
}
