using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Executors;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Handlers;
using Novin.Bpmn.Models;

namespace Novin.Bpmn;

public class BpmnEngine
{
    private readonly object _pauseLock = new();

    // Constructor for starting a new process
    public BpmnEngine(string path, string? processId = null)
    {
        var bpmnFileHandler = new BpmnFileDeserializer();
        ScriptHandler = new ScriptHandler();
        ScriptExecutor = new ScriptTaskExecutor();
        ServiceTaskExecutor = new ServiceTaskExecutor();
        UserTaskExecutor = new UserTaskExecutor();
        var definition = bpmnFileHandler.Deserialize(path);

        DefinitionsHandler = new BpmnDefinitionsHandler(definition);


        State = new BpmnState(definition, processId ?? DefinitionsHandler.GetFirstProcess().id);

        if (State.NodeQueue.Count != 0) return;

        var startEvent = DefinitionsHandler.GetStartEventForProcess(State.ProcessId);
        var startNode = CreateNewNode(startEvent, Guid.NewGuid().ToString(), true);
        State.NodeQueue.Enqueue(startNode);
    }

    // Constructor for continuing from a saved state
    public BpmnEngine(string path, BpmnState state)
    {
        var bpmnFileHandler = new BpmnFileDeserializer();
        var definition = bpmnFileHandler.Deserialize(path);
        ScriptHandler = new ScriptHandler();
        ScriptExecutor = new ScriptTaskExecutor();
        ServiceTaskExecutor = new ServiceTaskExecutor();
        UserTaskExecutor = new UserTaskExecutor();
        DefinitionsHandler = new BpmnDefinitionsHandler(definition);
        State = state;

        if (State.NodeQueue.Count != 0) return;

        var startEvent = DefinitionsHandler.GetStartEventForProcess(State.ProcessId);
        var startNode = CreateNewNode(startEvent,  Guid.NewGuid().ToString(),true);
        State.NodeQueue.Enqueue(startNode);
    }


    public BpmnDefinitionsHandler DefinitionsHandler { get; }
    public BpmnState State { get; }
    public IExecutor ScriptExecutor { get; }
    public IExecutor UserTaskExecutor { get; }
    public IExecutor ServiceTaskExecutor { get; }
    public ScriptHandler ScriptHandler { get; }


    public BpmnNode CreateNewNode(BpmnFlowElement element, string nodeToken, bool isExecutable , string? sourceToken = null)
    {
        lock (State.NodeStack)
        {
            BpmnNode node;

            if (State.NodeStack.Any(x => x.Id.Equals(element.id) && x.IsExpired == false))
                node = State.NodeStack.Peek();
            else
                node = new BpmnNode
                {
                    Id = element.id,
                    OutgoingTargets = DefinitionsHandler.GetOutgoingSequenceFlows(element),
                    IncommingFlows = DefinitionsHandler.GetIncomingSequenceFlows(element)
                };

            node.Tokens.Add(nodeToken);
            
            node.AddTransition(sourceToken, nodeToken, DateTime.Now, true); 
            node.Forks.Push(new Tuple<string, bool>(nodeToken, isExecutable));

            return node;
        }
    }


    public async Task StartProcess(bool immediately = true)
    {
        if (State.IsStopped)
            return;
        await WaitIfPaused();

        if (immediately)
        {
            while (State.NodeQueue.Any())
                try
                {
                    var nodeToProcess = State.NodeQueue.Peek();
                    await ProcessNode(nodeToProcess);
                    State.NodeQueue.Dequeue();
                }
                catch (Exception e)
                {
                    HandleExceptions(e);
                }
        }
        else
        {
            if (State.NodeQueue.Count != 0)
                try
                {
                    var nodeToProcess = State.NodeQueue.Peek();
                    await ProcessNode(nodeToProcess);
                    State.NodeQueue.Dequeue();
                }
                catch (Exception e)
                {
                    HandleExceptions(e);
                }
        }
    }

    private void HandleExceptions(Exception exception)
    {
        State.Exceptions.Push(exception.Message);
        Console.WriteLine(exception);
        throw exception;
    }

    private async Task ProcessNode(BpmnNode node)
    {
        if (State.IsStopped)
            return;

        await WaitIfPaused();

        try
        {
            node.Details = $"Executed at {DateTime.Now}";

            if (node.IsExecutable) await ExecuteTask(node);
            if (node.CanBeContinue) await FindNextNodes(node);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing node {node.Id}: {ex.Message}");
        }
    }

    private async Task ExecuteTask(BpmnNode node)
    {
        var element = DefinitionsHandler.GetElementById(node.Id);

        if (node.IsExecutable)
            switch (element)
            {
                case BpmnScriptTask _:
                    await ScriptExecutor.ExecuteAsync(node, this);
                    break;
                case BpmnServiceTask _:
                    await ServiceTaskExecutor.ExecuteAsync(node, this);
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
            foreach (var target in node.OutgoingTargets)
            {
                var newToken = Guid.NewGuid().ToString();
                var newNode = CreateNewNode(DefinitionsHandler.GetElementById(target.targetRef),newToken,node.IsExecutable,node.Tokens.First());
                EnqueueNode(newNode);

                // Add outgoing transition
                node
                    .AddTransition(node.Tokens.First(), newToken, DateTime.Now, false);

                node.IsExpired = true;
            }
        }
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
            await FindNextNodes(node);
            Console.WriteLine($"Completed {taskId}");
        }
    }

    // public List<string> GetExecutedPathsWithFlows()
    // {
    //     var executedPaths = new List<string>();
    //
    //     foreach (var node in State.Nodes.Values)
    //     foreach (var instance in node.Instances)
    //         if (instance.IsExecutable)
    //         {
    //             var currentPath = new List<string> { node.Id };
    //
    //             foreach (var transition in instance.OutgoingTransitions)
    //             {
    //                 var targetNode = State.Nodes.Values.FirstOrDefault(n =>
    //                     n.Instances.Any(inst => inst.Tokens.Contains(transition.TargetToken) && inst.IsExecutable));
    //                 if (targetNode != null)
    //                 {
    //                     currentPath.Add(transition.FlowSequenceId);
    //                     currentPath.Add(targetNode.Id);
    //                 }
    //             }
    //
    //             executedPaths.AddRange(currentPath);
    //         }
    //
    //     return executedPaths;
    // }
}