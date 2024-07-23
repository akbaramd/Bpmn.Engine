using Novin.Bpmn;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Executors.Abstracts;
using Novin.Bpmn.Handlers;
using Novin.Bpmn.Models;

public class BpmnProcessEngine
    {
        private readonly IUserAccessor userAccessor;
        private readonly ITaskStorage taskStorage;
        private readonly object _pauseLock = new();

        public BpmnProcessEngine(
            IUserAccessor userAccessor,
            ITaskStorage taskStorage,
            BpmnDefinitionsHandler definitionsHandler,
            BpmnProcessState? processState,
            IExecutor scriptExecutor,
            IExecutor userTaskExecutor,
            IExecutor serviceTaskExecutor)
        {
            this.userAccessor = userAccessor;
            this.taskStorage = taskStorage;
            DefinitionsHandler = definitionsHandler;
            ProcessState = processState;
            ScriptHandler = new ScriptHandler();
            ScriptExecutor = scriptExecutor;
            UserTaskExecutor = userTaskExecutor;
            ServiceTaskExecutor = serviceTaskExecutor;

            InitializeState();
        }

        public BpmnDefinitionsHandler DefinitionsHandler { get; }
        public BpmnProcessState? ProcessState { get; }
        public IExecutor ScriptExecutor { get; }
        public IExecutor UserTaskExecutor { get; }
        public IExecutor ServiceTaskExecutor { get; }
        public ScriptHandler ScriptHandler { get; }

        private void InitializeState()
        {
            if (ProcessState.NodeQueue.Count != 0) return;

            var startEvent = DefinitionsHandler.GetStartEventForProcess(ProcessState.ProcessElementId);
            var startNode = CreateNewNode(startEvent, Guid.NewGuid(), true);
            ProcessState.NodeQueue.Enqueue(startNode);
        }

        public BpmnProcessNode CreateNewNode(BpmnFlowElement element, Guid nodeId, bool isExecutable, BpmnProcessNode? sourceNode = null, BpmnSequenceFlow? flow = null)
        {
            lock (ProcessState.NodeStack)
            {
                var existingNode = ProcessState.NodeStack.FirstOrDefault(x => x.ElementId.Equals(element.id) && !x.IsExpired);

                var node = existingNode ?? new BpmnProcessNode
                {
                    ElementId = element.id,
                    Id = nodeId,
                    OutgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(element),
                    IncomingFlows = DefinitionsHandler.GetIncomingSequenceFlows(element)
                };

                if (existingNode == null) ProcessState.NodeStack.Push(node);

                if (sourceNode is not null)
                {
                    if (flow != null && isExecutable)
                        ProcessState.TransitionStack.Push(new BpmnNodeTransition
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
            if (ProcessState.IsStopped)
                return;
            await WaitIfPaused();

            if (immediately)
            {
                while (ProcessState.NodeQueue.Count != 0 && ProcessState.NodeQueue.Any(x=>x.UserTask == null || x.UserTask.IsCompleted))
                    try
                    {
                        var nodeToProcess = ProcessState.NodeQueue.Peek();
                        await ProcessNode(nodeToProcess);

                    }
                    catch (Exception e)
                    {
                        HandleExceptions(e);
                    }
            }
            else
            {
                if (ProcessState.NodeQueue.Count != 0)
                    try
                    {
                        var nodeToProcess = ProcessState.NodeQueue.Peek();
                        await ProcessNode(nodeToProcess);
                        ProcessState.NodeQueue.Dequeue();
                    }
                    catch (Exception e)
                    {
                        HandleExceptions(e);
                    }
            }
        }

        private void HandleExceptions(Exception exception)
        {
            ProcessState.Exceptions.Push(exception.Message);
            Console.WriteLine(exception);
            throw exception;
        }

        private async Task ProcessNode(BpmnProcessNode processNode)
        {
            if (ProcessState.IsStopped)
                return;

            await WaitIfPaused();

            try
            {
                processNode.Details = $"Executed at {DateTime.Now}";

                if (processNode.IsExecutable) await ExecuteTask(processNode);
                if (processNode.CanBeContinue) await FindNextNodes(processNode);
                
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
                        ProcessState.IsFinished = true;
                        ProcessState.IsStopped = true;
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
                    processNode.Merges.Push(new ValueTuple<string, Guid, bool>(processNode.ElementId, processNode.Id, processNode.IsExecutable));
                }
            }
            ProcessState!.NodeQueue.Dequeue();
        }

        public void EnqueueNode(BpmnProcessNode processNode)
        {
            lock (ProcessState!.NodeQueue)
            {
                ProcessState.NodeQueue.Enqueue(processNode);
            }
        }

        public void Pause()
        {
            lock (_pauseLock)
            {
                ProcessState.IsPaused = true;
            }
        }

        public void Resume()
        {
            lock (_pauseLock)
            {
                ProcessState.IsPaused = false;
                Monitor.PulseAll(_pauseLock);
            }
        }

        public void Stop()
        {
            ProcessState.IsStopped = true;
        }

        private async Task WaitIfPaused()
        {
            await Task.Run(() =>
            {
                lock (_pauseLock)
                {
                    while (ProcessState.IsPaused) Monitor.Wait(_pauseLock);
                }
            });
        }

        public string ExportStateAsJson()
        {
            return ProcessState.SaveState();
        }

        public async Task CompleteUserTask(string taskId)
        {
            var node = ProcessState?.NodeStack.FirstOrDefault(x => x.UserTask is not null && x.UserTask.TaskId.Equals(taskId));
            if (node is not null)
            {
                node.UserTask!.IsCompleted = true;
                await FindNextNodes(node);
                Console.WriteLine($"Completed {taskId}");
            }
        }

        public List<ElementState> GetExecutedPathsWithFlows()
        {
            var executedPaths = new List<ElementState>();

            foreach (var node in ProcessState.NodeStack.Reverse())
            {
                executedPaths.Add(new ElementState
                {
                    ElementId = node.ElementId,
                    IsActive = node.IsExecutable &&
                               (node.IncomingFlows.Count == 0 || node.IncomingFlows.Count() == node.Instances.Count),
                    HasUserTask = node.UserTask is not null
                });

                var transitions = ProcessState.TransitionStack.Where(x => x.SourceNodeId.Equals(node.Id));

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
