using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;
using Novin.Bpmn.Handlers;
using Novin.Bpmn.Models;

namespace Novin.Bpmn
{
    public partial class BpmnProcessExecutor
    {
        private readonly IBpmnProcessAccessor _bpmnProcessAccessor;
        private readonly IBpmnTaskAccessor _bpmnTaskAccessor;
        private readonly ReaderWriterLockSlim _queueLock = new();
        private readonly object _pauseLock = new();
        public BpmnDefinitionsHandler DefinitionsHandler;

        public BpmnProcessExecutor(
            IBpmnTaskAccessor bpmnTaskAccessor,
            IBpmnProcessAccessor bpmnProcessAccessor,
            IScriptTaskExecutor scriptExecutor,
            IUserTaskExecutor userTaskExecutor,
            IServiceTaskExecutor serviceTaskExecutor)
        {
            _bpmnTaskAccessor = bpmnTaskAccessor;
            _bpmnProcessAccessor = bpmnProcessAccessor;
            ScriptHandler = new ScriptHandler();
            ScriptExecutor = scriptExecutor;
            UserTaskExecutor = userTaskExecutor;
            ServiceTaskExecutor = serviceTaskExecutor;
            
        }

        public BpmnProcessInstance Instance { get; set; }
        public IExecutor ScriptExecutor { get; }
        public IExecutor UserTaskExecutor { get; }
        public IExecutor ServiceTaskExecutor { get; }
        public ScriptHandler ScriptHandler { get; }
        public BpmnRouter Router { get; set; }
        public void Initialize(string deploymentKey, string deploymentXml)
        {
            DefinitionsHandler = new BpmnDefinitionsHandler(deploymentXml);
            Router = new BpmnRouter(DefinitionsHandler);
            Instance = new BpmnProcessInstance(deploymentXml);

            Instance.SetDeploymentKey(deploymentKey);
            Instance.SetDefinitionXml(deploymentXml);
            
            if (!Instance.IsInProgress())
            {
                InitializeState();
            }
        }

        

        public void Initialize(BpmnProcessInstance instance)
        {
            DefinitionsHandler = new BpmnDefinitionsHandler(instance.DefinitionXml);
            Instance = instance;
            Router = new BpmnRouter(DefinitionsHandler);
            
            if (!Instance.IsInProgress())
            {
                InitializeState();
            }
        }

        public void InitializeState()
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

        public void StoreProcessState()
        {
            _bpmnProcessAccessor.StoreProcessState(Instance.DeploymentKey, Instance.Id, Instance);
        }
    }
}
