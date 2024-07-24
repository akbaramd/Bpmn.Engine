using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Handlers;
using Novin.Bpmn.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn;

public class BpmnProcessEngine
{
    private readonly IBpmnProcessAccessor _bpmnProcessAccessor;
    private readonly IBpmnTaskAccessor _bpmnTaskAccessor;
    private readonly IBpmnUserAccessor _bpmnUserAccessor;
    public readonly BpmnDefinitionsHandler DefinitionsHandler;
    private readonly object _pauseLock = new();

    public BpmnProcessEngine(
        string deploymentXml,
        string deploymentKey,
        IBpmnUserAccessor bpmnUserAccessor,
        IBpmnTaskAccessor bpmnTaskAccessor,
        IBpmnProcessAccessor bpmnProcessAccessor,
        IExecutor scriptExecutor,
        IExecutor userTaskExecutor,
        IExecutor serviceTaskExecutor)
    {

        _bpmnUserAccessor = bpmnUserAccessor;
        _bpmnTaskAccessor = bpmnTaskAccessor;
        _bpmnProcessAccessor = bpmnProcessAccessor;
        DefinitionsHandler = new BpmnDefinitionsHandler(deploymentXml);
        Instance = new BpmnProcessInstance(deploymentXml, DefinitionsHandler.GetFirstProcess().id);
        ScriptHandler = new ScriptHandler();
        ScriptExecutor = scriptExecutor;
        UserTaskExecutor = userTaskExecutor;
        ServiceTaskExecutor = serviceTaskExecutor;

        InitializeInstance(deploymentKey, deploymentXml);
        InitializeState();
    }

    public BpmnProcessEngine(
        BpmnProcessInstance instance,
        IBpmnUserAccessor bpmnUserAccessor,
        IBpmnTaskAccessor bpmnTaskAccessor,
        IBpmnProcessAccessor bpmnProcessAccessor,
        IExecutor scriptExecutor,
        IExecutor userTaskExecutor,
        IExecutor serviceTaskExecutor)
    {
        DefinitionsHandler = new BpmnDefinitionsHandler(instance.Definition);
        _bpmnUserAccessor = bpmnUserAccessor;
        _bpmnTaskAccessor = bpmnTaskAccessor;
        _bpmnProcessAccessor = bpmnProcessAccessor;
        Instance = instance;
        ScriptHandler = new ScriptHandler();
        ScriptExecutor = scriptExecutor;
        UserTaskExecutor = userTaskExecutor;
        ServiceTaskExecutor = serviceTaskExecutor;

        InitializeState();
    }

    public BpmnProcessInstance Instance { get; }
    public IExecutor ScriptExecutor { get; }
    public IExecutor UserTaskExecutor { get; }
    public IExecutor ServiceTaskExecutor { get; }
    public ScriptHandler ScriptHandler { get; }

    private void InitializeInstance(string deploymentKey, string deploymentXml)
    {
        if (Instance != null)
        {
            Instance.SetDeploymentKey(deploymentKey);
            Instance.SetDefinitionXml(deploymentXml);
        }
    }

    private void InitializeState()
    {
        if (Instance.NodeQueue.Count != 0 || Instance.NodeStack.Count != 0) return;

        var startEvent = DefinitionsHandler.GetStartEventForProcess(Instance.ProcessElementId);
        var startNode = CreateNewNode(startEvent, Guid.NewGuid(), true);
        Instance.NodeQueue.Enqueue(startNode);
        StoreProcessState();
    }

    public BpmnProcessNode CreateNewNode(BpmnFlowElement element, Guid nodeId, bool isExecutable,
        BpmnProcessNode sourceNode = null, BpmnSequenceFlow flow = null)
    {
        lock (Instance.NodeStack)
        {
            var existingNode = Instance.NodeStack.FirstOrDefault(x => x.ElementId.Equals(element.id) && !x.IsExpired);

            var node = existingNode ?? new BpmnProcessNode(element.id, nodeId,DefinitionsHandler.GetIncomingSequenceFlows(element),DefinitionsHandler.GetOutgoingSequenceFlows(element));


            if (existingNode == null) Instance.NodeStack.Push(node);

            if (sourceNode != null)
            {
                if (flow != null && isExecutable)
                    Instance.TransitionStack.Push(new BpmnNodeTransition
                    {
                        Id = Guid.NewGuid(),
                        ElementId = flow.id,
                        TransitionTime = DateTime.Now,
                        SourceNodeId = sourceNode.Id,
                        TargetNodeId = node.Id
                    });

                node.AddInstance(sourceNode.ElementId, sourceNode.Id, node.Id, isExecutable);
                StoreProcessState();
                return node;
            }

            node.AddInstance("", Guid.Empty, nodeId, isExecutable);
            StoreProcessState();
            return node;
        }
    }

    public async Task<BpmnProcessInstance> StartProcess(bool immediately = true)
    {
        if (Instance.IsStopped) return Instance;

        await WaitIfPaused();

        if (immediately)
        {

            while (Instance.NodeQueue.Count != 0)
            {
                var nodeToProcess = Instance.NodeQueue.Peek();

                try
                {
                    await ProcessNode(nodeToProcess);
                    StoreProcessState();
                    Instance.NodeQueue.Dequeue();
                    
                }
                catch (Exception e)
                {
                    HandleExceptions(e);
                }
            }
            
        }
        else
        {
            while (Instance.NodeQueue.Count != 0)
            {
                var nodeToProcess = Instance.NodeQueue.Peek();

                if (nodeToProcess.UserTask != null && !nodeToProcess.UserTask.IsCompleted)
                {
                    // Skip this node and move it to the end of the queue
                    Instance.NodeQueue.Enqueue(Instance.NodeQueue.Dequeue());
                    continue;
                }

                try
                {
                    await ProcessNode(nodeToProcess);
                    Instance.NodeQueue.Dequeue(); // Remove the processed node from the queue
                    StoreProcessState();
                }
                catch (Exception e)
                {
                    HandleExceptions(e);
                }
            }
        }

        return Instance;
    }
    private void HandleExceptions(Exception exception)
    {
        Instance.Exceptions.Push(exception.Message);
        Console.WriteLine(exception);
        throw exception;
    }

    private async Task ProcessNode(BpmnProcessNode processNode)
    {
        if (Instance.IsStopped) return;

        await WaitIfPaused();

        try
        {
            processNode.Details = $"Executed at {DateTime.Now}";

            if (processNode.IsExecutable) await ExecuteTask(processNode);
            if (processNode.CanBeContinue) await FindNextNodes(processNode);
            StoreProcessState();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing node {processNode.ElementId}: {ex.Message}");
        }
    }

    private async Task ExecuteTask(BpmnProcessNode processNode)
    {
        var element = DefinitionsHandler.GetElementById(processNode.ElementId);

        if (processNode.IsExecutable)
            switch (element)
            {
                case BpmnScriptTask _:
                    await ScriptExecutor.ExecuteAsync(processNode, this);
                    break;
                case BpmnServiceTask _:
                    await ServiceTaskExecutor.ExecuteAsync(processNode, this);
                    break;
                case BpmnUserTask _:
                    await UserTaskExecutor.ExecuteAsync(processNode, this);
                    return;
                case BpmnEndEvent _:
                    Instance.Finish();
                    StoreProcessState();
                    return;
            }
    }

    private async Task FindNextNodes(BpmnProcessNode processNode)
    {
        var element = DefinitionsHandler.GetElementById(processNode.ElementId);
        if (element is BpmnGateway gateway)
        {
            IGatewayHandler? handler = gateway switch
            {
                BpmnInclusiveGateway _ => new InclusiveGatewayHandler(),
                BpmnExclusiveGateway _ => new ExclusiveGatewayHandler(),
                BpmnParallelGateway _ => new ParallelGatewayHandler(),
                _ => null
            };

            if (handler != null) await handler.HandleGateway(processNode, this);
        }
        else
        {
            foreach (var target in processNode.OutgoingFlows)
            {
                var newToken = Guid.NewGuid();
                var newNode = CreateNewNode(DefinitionsHandler.GetElementById(target.targetRef), newToken,
                    processNode.IsExecutable, processNode, target);
                EnqueueNode(newNode);
                processNode.Expire();
                processNode.AddMerge(processNode.ElementId, processNode.Id, processNode.IsExecutable);
                StoreProcessState();
            }
        }

        
    }

    public void EnqueueNode(BpmnProcessNode processNode)
    {
        lock (Instance.NodeQueue)
        {
            Instance.NodeQueue.Enqueue(processNode);
            StoreProcessState();
        }
    }

    public void Pause()
    {
        lock (_pauseLock)
        {
            Instance.Pause();
            StoreProcessState();
        }
    }

    public void Resume()
    {
        lock (_pauseLock)
        {
            Instance.Resume();
            Monitor.PulseAll(_pauseLock);
            StoreProcessState();
        }
    }

    public void Stop()
    {
        Instance.Stop();
        StoreProcessState();
    }

    private async Task WaitIfPaused()
    {
        await Task.Run(() =>
        {
            lock (_pauseLock)
            {
                while (Instance.IsPaused) Monitor.Wait(_pauseLock);
            }
        });
    }

    public string ExportStateAsJson()
    {
        return Instance.SaveState();
    }

    public async Task CompleteUserTask(string taskId)
    {
        var node = Instance.NodeStack.FirstOrDefault(x => x.UserTask != null && x.UserTask.TaskId.Equals(taskId));
        if (node != null)
        {
            node.UserTask.CompleteTask();
            await _bpmnTaskAccessor.StoreTask(node.UserTask);
            await FindNextNodes(node);
            StoreProcessState();
            Console.WriteLine($"Completed {taskId}");
        }
    }

    private void StoreProcessState()
    {
        _bpmnProcessAccessor.StoreProcessState(Instance.DeploymentKey, Instance.Id, Instance);
    }
}
