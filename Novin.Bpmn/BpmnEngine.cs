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
        var startNode = CreateNewNode(startEvent, Guid.NewGuid(), true);
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
        var startNode = CreateNewNode(startEvent, Guid.NewGuid(), true);
        State.NodeQueue.Enqueue(startNode);
    }


    public BpmnDefinitionsHandler DefinitionsHandler { get; }
    public BpmnState State { get; }
    public IExecutor ScriptExecutor { get; }
    public IExecutor UserTaskExecutor { get; }
    public IExecutor ServiceTaskExecutor { get; }
    public ScriptHandler ScriptHandler { get; }

    public BpmnNode CreateNewNode(BpmnFlowElement element, Guid nodeId, bool isExecutable, BpmnNode? sourceNode = null,
        BpmnSequenceFlow? flow = null)
    {
        lock (State.NodeStack)
        {
            var existingNode = State.NodeStack.FirstOrDefault(x => x.ElementId.Equals(element.id) && !x.IsExpired);

            var node = existingNode ?? new BpmnNode
            {
                ElementId = element.id,
                Id = nodeId,
                OutgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(element),
                IncomingFlows = DefinitionsHandler.GetIncomingSequenceFlows(element)
            };

            if (existingNode == null) State.NodeStack.Push(node);

            // node.AddTransition(sourceNode?.Id ?? Guid.Empty, nodeId, DateTime.Now, true);
            if (sourceNode is not null)
            {
                if (flow != null && isExecutable)
                    State.TransitionStack.Push(new BpmnNodeTransition
                    {
                        Id = Guid.NewGuid(),
                        ElementId = flow.id,
                        TransitionTime = DateTime.Now,
                        SourceNodeId = sourceNode.Id,
                        TargetNodeId = node.Id
                    });
                node.Instances.Push(
                    new ValueTuple<string, Guid, Guid, bool>(sourceNode.ElementId, sourceNode.Id, node.Id,
                        isExecutable));
                return node;
            }

            node.Instances.Push(new ValueTuple<string, Guid, Guid, bool>("", Guid.Empty, nodeId, isExecutable));
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
            while (State.NodeQueue.Count != 0)
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
            Console.WriteLine($"Error processing node {node.ElementId}: {ex.Message}");
        }
    }

    private async Task ExecuteTask(BpmnNode node)
    {
        var element = DefinitionsHandler.GetElementById(node.ElementId);

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
                case BpmnEndEvent _:
                    State.IsFinished = true;
                    State.IsStopped = true;
                    return;
            }
    }

    private async Task FindNextNodes(BpmnNode node)
    {
        var element = DefinitionsHandler.GetElementById(node.ElementId);
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
            foreach (var target in node.OutgoingFlows)
            {
                var newToken = Guid.NewGuid();
                var newNode = CreateNewNode(DefinitionsHandler.GetElementById(target.targetRef), newToken,
                    node.IsExecutable, node, target);
                EnqueueNode(newNode);
                node.IsExpired = true;
                node.Merges.Push(new ValueTuple<string, Guid, bool>(node.ElementId, node.Id, node.IsExecutable));
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


    public List<ElementState> GetExecutedPathsWithFlows()
    {
        var executedPaths = new List<ElementState>();


        foreach (var node in State.NodeStack.Reverse())
        {
            executedPaths.Add(new ElementState
            {
                ElementId = node.ElementId,
                IsActive = node.IsExecutable && (node.IncomingFlows.Count == 0 || node.IncomingFlows.Count() == node.Instances.Count),
                HasUserTask = State.WaitingUserTasks.Any(x => x.Key.Equals(node.ElementId))
            });


            var transitions = State.TransitionStack.Where(x => x.SourceNodeId.Equals(node.Id));
            foreach (var transition in transitions)
                executedPaths.Add(new ElementState
                {
                    ElementId = transition.ElementId,
                    IsActive = node.IsExecutable && node.IncomingFlows.Count == node.Instances.Count,
                    HasUserTask = false
                });
        }
        return executedPaths;
    }
}

public class ElementState
{
    public string ElementId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool HasUserTask { get; set; }
}