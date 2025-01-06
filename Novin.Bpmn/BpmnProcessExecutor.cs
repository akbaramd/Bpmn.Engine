using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Handlers;
using Novin.Bpmn.Models;

namespace Novin.Bpmn;

public class BpmnProcessExecutor
{
    private readonly IBpmnProcessAccessor _bpmnProcessAccessor;
    private readonly IBpmnTaskAccessor _bpmnTaskAccessor;
    private readonly ReaderWriterLockSlim _queueLock = new();
    private readonly object _pauseLock = new();
    public readonly BpmnDefinitionsHandler DefinitionsHandler;
    private readonly ITimerHandler _boundaryEventHandler;
    public BpmnProcessExecutor(
        string deploymentXml,
        string deploymentKey,
        IBpmnTaskAccessor bpmnTaskAccessor,
        IBpmnProcessAccessor bpmnProcessAccessor,
        IScriptTaskExecutor scriptExecutor,
        IUserTaskExecutor userTaskExecutor,
        IServiceTaskExecutor serviceTaskExecutor,
        ITimerHandler boundaryEventHandler)
    {
        _bpmnTaskAccessor = bpmnTaskAccessor;
        _bpmnProcessAccessor = bpmnProcessAccessor;
        _boundaryEventHandler = boundaryEventHandler;
        DefinitionsHandler = new BpmnDefinitionsHandler(deploymentXml);
        Instance = new BpmnProcessInstance(deploymentXml, DefinitionsHandler.GetFirstProcess().id);
        ScriptHandler = new ScriptHandler();
        ScriptExecutor = scriptExecutor;
        UserTaskExecutor = userTaskExecutor;
        ServiceTaskExecutor = serviceTaskExecutor;

        InitializeInstance(deploymentKey, deploymentXml);
        InitializeState();
    }

    public BpmnProcessExecutor(
        BpmnProcessInstance instance,
        IBpmnTaskAccessor bpmnTaskAccessor,
        IBpmnProcessAccessor bpmnProcessAccessor,
        IScriptTaskExecutor scriptExecutor,
        IUserTaskExecutor userTaskExecutor,
        IServiceTaskExecutor serviceTaskExecutor,
        ITimerHandler boundaryEventHandler)
    {
        _boundaryEventHandler = boundaryEventHandler;
        DefinitionsHandler = new BpmnDefinitionsHandler(instance.Definition);
        _bpmnTaskAccessor = bpmnTaskAccessor;
        _bpmnProcessAccessor = bpmnProcessAccessor;
        Instance = instance;
        ScriptHandler = new ScriptHandler();
        ScriptExecutor = scriptExecutor;
        UserTaskExecutor = userTaskExecutor;
        ServiceTaskExecutor = serviceTaskExecutor;

        if (!Instance.IsInProgress())
        {
            InitializeState();
        }
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
        if (Instance.NextQueue.Count != 0 || Instance.NodeStack.Count != 0) return;

        var startEvents = DefinitionsHandler.GetStartEventsForProcess(Instance.ProcessElementId);

        if (startEvents == null || !startEvents.Any())
        {
            throw new InvalidOperationException($"No start events found for process {Instance.ProcessElementId}.");
        }

        foreach (var startEvent in startEvents)
        {
            var startNode = CreateNewNode(startEvent, Guid.NewGuid(), true);
            EnqueueNext(startNode);
        }

        StoreProcessState();
    }

    public BpmnProcessNode CreateNewNode(BpmnFlowElement element, Guid nodeId, bool isExecutable,
        BpmnProcessNode sourceNode = null, BpmnSequenceFlow flow = null)
    {
        lock (Instance.NodeStack)
        {
            var existingNode = Instance.NodeStack.FirstOrDefault(x => x.ElementId.Equals(element.id) && !x.IsExpired);

            var node = existingNode ?? new BpmnProcessNode(element.id, nodeId,
                DefinitionsHandler.GetIncomingSequenceFlows(element),
                DefinitionsHandler.GetOutgoingSequenceFlows(element));

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
            while (Instance.NextQueue.Count != 0)
            {
                var nodeToProcess = DequeueNext();

                try
                {
                    await ProcessNodeWithRetries(nodeToProcess);
                    StoreProcessState();
                }
                catch (Exception e)
                {
                    HandleNodeException(nodeToProcess, e);
                    MoveToFailedQueue(nodeToProcess);
                }
            }
        }
        else
        {
            while (Instance.NextQueue.Count != 0)
            {
                var nodeToProcess = PeekNext();

                if (nodeToProcess.UserTask != null && !nodeToProcess.UserTask.IsCompleted)
                {
                    EnqueueNext(DequeueNext()); // Skip incomplete user task
                    continue;
                }

                try
                {
                    await ProcessNodeWithRetries(nodeToProcess);
                    DequeueNext();
                    StoreProcessState();
                }
                catch (Exception e)
                {
                    HandleNodeException(nodeToProcess, e);
                    MoveToFailedQueue(nodeToProcess);
                }
            }
        }

        return Instance;
    }

    private async Task ProcessNodeWithRetries(BpmnProcessNode processNode, int maxRetries = 3)
    {
        int attempts = 0;
        while (attempts < maxRetries)
        {
            try
            {
                await ProcessNode(processNode);
                return; // Success, exit retry loop
            }
            catch (Exception ex)
            {
                attempts++;
                if (attempts >= maxRetries)
                    throw; // Rethrow after exhausting retries
            }
        }
    }

    private void MoveToFailedQueue(BpmnProcessNode processNode)
    {
        _queueLock.EnterWriteLock();
        try
        {
            Instance.FailedQueue.Add(processNode);
            Console.WriteLine($"Node {processNode.ElementId} moved to FailedQueue.");
        }
        finally
        {
            _queueLock.ExitWriteLock();
        }
    }

    public void ResumeFailedNodes()
    {
        _queueLock.EnterWriteLock();
        try
        {
            foreach (var node in Instance.FailedQueue.ToList())
            {
                Instance.FailedQueue.Remove(node);
                EnqueueNext(node);
            }

            Console.WriteLine("Resumed all failed nodes.");
        }
        finally
        {
            _queueLock.ExitWriteLock();
        }
    }

    private void HandleNodeException(BpmnProcessNode processNode, Exception exception)
    {
        processNode.Exceptions.Add(exception.InnerException?.Message ?? exception.Message);
        Console.WriteLine($"Error in node {processNode.ElementId}: {exception.Message}");
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
            HandleNodeException(processNode, ex);
            throw;
        }
    }





    private async Task ExecuteUserTask(BpmnProcessNode processNode)
    {
        await UserTaskExecutor.ExecuteAsync(processNode, this);
        // Add user task-specific logic if needed
    }
    private async Task ExecuteScriptTask(BpmnProcessNode processNode)
    {
        await ScriptExecutor.ExecuteAsync(processNode, this);
        // Add user task-specific logic if needed
    }
    public async Task FindNextNodes(BpmnProcessNode processNode)
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
                EnqueueNext(newNode);
                processNode.Expire();
                await _boundaryEventHandler.CancelTimer(processNode);
                processNode.AddMerge(processNode.ElementId, processNode.Id, processNode.IsExecutable);
                StoreProcessState();
            }
        }
      
    }
private async Task ExecuteTask(BpmnProcessNode processNode)
{

    var attachedEvents = DefinitionsHandler.GetAttachedEvents(processNode.ElementId);

    if (attachedEvents != null)
    {
        foreach (var boundaryEvent in attachedEvents)
        {
            await _boundaryEventHandler.ExecuteAsync(boundaryEvent,processNode, this);
            
            
        }
    }

    // اجرای فعالیت اصلی
    await ManageMainActivity(processNode);
}
private async Task SafeMoveToNextElement(BpmnProcessNode expiredNode)
{
    try
    {
   

        // Logic to find and enqueue the next element
        var nextFlows = expiredNode.OutgoingFlows;
        foreach (var flow in nextFlows)
        {
            var nextElement = DefinitionsHandler.GetElementById(flow.targetRef);
            var nextNode = new BpmnProcessNode(nextElement.id, Guid.NewGuid(), null, null);

            Console.WriteLine($"Enqueuing next node {nextNode.ElementId}");
            EnqueueNext(nextNode); // Custom logic to enqueue the next node
      
        }
        
        await StartProcess();
    }
    catch (ObjectDisposedException ex)
    {
        Console.WriteLine($"Error: Accessed a disposed object. Details: {ex.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Unexpected error in SafeMoveToNextElement: {ex.Message}");
    }

    await Task.CompletedTask;
}
private async Task ManageMainActivity(BpmnProcessNode processNode)
{
    try
    {
        var element = DefinitionsHandler.GetElementById(processNode.ElementId);

        switch (element)
        {
            case BpmnUserTask userTask:
                Console.WriteLine($"Starting user task {userTask.id}");
                await ExecuteUserTask(processNode);
                break;
            case BpmnScriptTask scriptTask:
                Console.WriteLine($"Starting user task {scriptTask.id}");
                await ExecuteScriptTask(processNode);
                break;
            case BpmnServiceTask serviceTask:
                Console.WriteLine($"Starting service task {serviceTask.id}");
                await ServiceTaskExecutor.ExecuteAsync(processNode, this);
                break;

            default:
                Console.WriteLine($"Unhandled task type for node {processNode.ElementId}");
                break;
        }

        Console.WriteLine($"Activity {processNode.ElementId} completed.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in main activity {processNode.ElementId}: {ex.Message}");
        throw;
    }
}



    public void EnqueueNext(BpmnProcessNode processNode)
    {
        _queueLock.EnterWriteLock();
        try
        {
            Instance.NextQueue.Enqueue(processNode);
        }
        finally
        {
            _queueLock.ExitWriteLock();
        }
    }

    public BpmnProcessNode DequeueNext()
    {
        _queueLock.EnterWriteLock();
        try
        {
            return Instance.NextQueue.Dequeue();
        }
        finally
        {
            _queueLock.ExitWriteLock();
        }
    }

    public BpmnProcessNode PeekNext()
    {
        _queueLock.EnterReadLock();
        try
        {
            return Instance.NextQueue.Peek();
        }
        finally
        {
            _queueLock.ExitReadLock();
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

    public async Task CompleteUserTask(Guid taskId)
    {
        var node = Instance.NodeStack.FirstOrDefault(x => x.UserTask != null && x.UserTask.TaskId.Equals(taskId));
        if (node != null)
        {
            node.UserTask!.CompleteTask();
            await _bpmnTaskAccessor.StoreTask(node.UserTask);

            await FindNextNodes(node);
            Instance.PendingQueue.Remove(Instance.PendingQueue.First(x =>
                x.UserTask != null && x.UserTask.TaskId.Equals(taskId)));
            StoreProcessState();
            Console.WriteLine($"Completed {taskId}");
        }
    }

    public void EnqueuePending(BpmnProcessNode processNode)
    {
        _queueLock.EnterWriteLock();
        try
        {
            Instance.PendingQueue.Add(processNode);
        }
        finally
        {
            _queueLock.ExitWriteLock();
        }
    }

    public void StoreProcessState()
    {
        _bpmnProcessAccessor.StoreProcessState(Instance.DeploymentKey, Instance.Id, Instance);
    }
}