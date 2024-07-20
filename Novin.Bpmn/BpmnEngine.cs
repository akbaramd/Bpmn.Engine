using Novin.Bpmn.Test.Abstractions;
using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Executors;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Handlers;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test;

public class BpmnEngine
{
    private readonly object _pauseLock = new();

    // Constructor for starting a new process
    public BpmnEngine(string path, string? processId = null)
    {
        var bpmnFileHandler = new BpmnFileDeserializer();
        ScriptHandler = new ScriptHandler();
        ScriptExecutor = new ScriptTaskExecutor();
        UserTaskExecutor = new UserTaskExecutor();
        var definition = bpmnFileHandler.Deserialize(path);

        DefinitionsHandler = new BpmnDefinitionsHandler(definition);


        State = new ProcessState(definition, processId ?? DefinitionsHandler.GetFirstProcess().id);
    }

    // Constructor for continuing from a saved state
    public BpmnEngine(string path, ProcessState state)
    {
        var bpmnFileHandler = new BpmnFileDeserializer();
        ScriptHandler = new ScriptHandler();
        ScriptExecutor = new ScriptTaskExecutor();
        UserTaskExecutor = new UserTaskExecutor();
        var definition = bpmnFileHandler.Deserialize(path);
        DefinitionsHandler = new BpmnDefinitionsHandler(definition);
        State = state;
    }


    public BpmnDefinitionsHandler DefinitionsHandler { get; }
    public ProcessState State { get; }
    public IExecutor ScriptExecutor { get; }
    public IExecutor UserTaskExecutor { get; }
    public ScriptHandler ScriptHandler { get; }


    public BpmnNode CreateNewNode(BpmnFlowElement element, string token, bool isExecutable, string sourceToken = null)
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
                if (currentInstance != null && currentInstance.Tokens.Contains(token)) return node;

                if (currentInstance == null || currentInstance.IsExpired)
                {
                    var newInstance = new BpmnNodeInstance
                    {
                        Timestamp = DateTime.Now
                    };
                    newInstance.Tokens.Add(token);
                    newInstance.AddTransition(sourceToken, token, DateTime.Now, true); // Incoming transition


                    newInstance.Forks.Push(new Tuple<string, bool>(token, isExecutable));


                    node.Instances.Push(newInstance);
                }
                else
                {
                    currentInstance.Tokens.Add(token);
                    currentInstance.AddTransition(sourceToken, token, DateTime.Now, true); // Incoming transition

                    currentInstance.Forks.Push(new Tuple<string, bool>(token, isExecutable));
                }
            }

            return node;
        }
    }


    public async Task StartProcess(bool immediately = true)
    {
        if (!State.NodeQueue.Any())
        {
            var startEvent = DefinitionsHandler.GetStartEventForProcess(State.ProcessId);
            var startNode = CreateNewNode(startEvent, Guid.NewGuid().ToString(), true);
            State.NodeQueue.Enqueue(startNode);
        }

        if (immediately)
        {
            while (State.NodeQueue.Any())
            {
                var nodeToProcess = State.NodeQueue.Dequeue();
                await ProcessNode(nodeToProcess);
            }
        }
        else
        {
            if (State.NodeQueue.Any())
            {
                var nodeToProcess = State.NodeQueue.Dequeue();
                await ProcessNode(nodeToProcess);
            }
        }
    }

    private async Task ProcessNode(BpmnNode node)
    {
        if (State.IsStopped)
            return;

        await WaitIfPaused();

        try
        {
            var currentInstance = node.Instances.Peek();
            currentInstance.Details = $"Executed at {DateTime.Now}";

            if (currentInstance.IsExecutable) await ExecuteTask(node);


            if (currentInstance.CanBeContinue) await FindNextNodes(node);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing node {node.Id}: {ex.Message}");
        }
    }

    private async Task ExecuteTask(BpmnNode node)
    {
        var element = DefinitionsHandler.GetElementById(node.Id);

        if (node.Instances.Peek().IsExecutable)
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

            if (handler != null) await handler.HandleGateway(node, this);
        }
        else
        {
            foreach (var flow in node.OutgoingFlows)
            {
                var targetToken = Guid.NewGuid().ToString();
                var newNode = CreateNewNode(DefinitionsHandler.GetElementById(flow.targetRef),
                    targetToken, node.Instances.Peek().IsExecutable, node.Instances.Peek().Tokens.First());
                EnqueueNode(newNode);

                // Add outgoing transition
                node.Instances.Peek()
                    .AddTransition(node.Instances.Peek().Tokens.First(), targetToken, DateTime.Now, false);

                node.Instances.Peek().IsExpired = true;
            }
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
                var targetToken = Guid.NewGuid().ToString();
                var newNode = CreateNewNode(DefinitionsHandler.GetElementById(flow.targetRef),
                    targetToken, node.Instances.Peek().IsExecutable);
                nextNodes.Add(newNode);

                // Add outgoing transition
                node.Instances.Peek()
                    .AddTransition(node.Instances.Peek().Tokens.First(), targetToken, DateTime.Now, false);
            }
        }

        return nextNodes;
    }

    public void EnqueueNode(BpmnNode node)
    {
        lock (State.NodeQueue)
        {
            State.NodeQueue.Enqueue(node);
        }
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
                while (State.IsPaused) Monitor.Wait(_pauseLock);
            }
        });
    }

    public string ExportStateAsJson()
    {
        return State.SaveState();
    }

    public async Task CompleteUserTask(string taskId)
    {
        if (State.WaitingUserTasks.TryRemove(taskId, out var node))
            // Console.WriteLine($"User task {node.Id} is completed.");
        {
            
            node.Instances.Peek().CanBeContinue = true;
            await FindNextNodes(node);
            Console.WriteLine($"Completed {taskId}");
        }
        
    }
}