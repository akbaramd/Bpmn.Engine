using Novin.Bpmn.Abstractions;
using Novin.Bpmn.V2.Abstractions;

namespace Novin.Bpmn.V2
{
    /// <summary>
    /// Executes a BPMN process instance using a queue-based mechanism (without boundary events).
    /// </summary>
    public class BpmnV2ProcessExecutor
    {
        private readonly object _pauseLock = new();
        private BpmnQueueManager _queueManager;
        private IBpmnProcessAccessor _processAccessor;
        private BpmnV2BoundaryEventHandler boundaryEventHandler;

        public BpmnProcessInstance Instance { get; private set; }
        public BpmnV2Router Router { get; }
        private readonly IBpmnV2TaskHandler _taskHandler;

        public BpmnV2ProcessExecutor(
            BpmnV2Router router,
            IBpmnV2TaskHandler taskHandler, BpmnV2BoundaryEventHandler boundaryEventHandler,
            IBpmnProcessAccessor processAccessor)
        {
            Router = router ?? throw new ArgumentNullException(nameof(router));
            _taskHandler = taskHandler ?? throw new ArgumentNullException(nameof(taskHandler));
            this.boundaryEventHandler = boundaryEventHandler;
            _processAccessor = processAccessor;
        }

        /// <summary>
        /// Initializes the executor with a BPMN process instance.
        /// </summary>
        public void Initialize(string key, string content)
        {
            Instance = new BpmnProcessInstance(content);
            Instance.SetDefinitionXml(content);
            Instance.SetDeploymentKey(key);
            
            Initialize(Instance);
        }

        /// <summary>
        /// Initializes the executor with a BPMN process instance.
        /// </summary>
        public void Initialize(BpmnProcessInstance instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            Instance = instance;
            _queueManager = new BpmnQueueManager(Instance);

            // اگر اینستنس خالی است (تازه شروع شده)، گره‌های استارت را به صف اضافه می‌کنیم
            if (!Instance.IsInProgress())
            {
                _queueManager.EnqueueStartNodes();
            }
        }

        /// <summary>
        /// Starts the BPMN process execution in a step-by-step or immediate fashion (without boundary events).
        /// </summary>
        public async Task<BpmnProcessInstance> StartProcessAsync(bool immediately = true,
            CancellationToken cancellationToken = default)
        {
            if (Instance.IsStopped) return Instance;

            await WaitIfPaused();

            while (_queueManager.Count > 0)
            {
                var nodeToProcess = _queueManager.Dequeue();

                // اگر کاربر-تسک کامل نشده و اجرای فوری نداریم
                if (!immediately && IsIncompleteUserTask(nodeToProcess))
                {
                    RequeueTask(nodeToProcess);
                    continue;
                }

                await ProcessNodeAsync(nodeToProcess, cancellationToken);
            }

            return Instance;
        }

        /// <summary>
        /// Processes a single node, executes the main task, and routes next nodes.
        /// </summary>
        private async Task ProcessNodeAsync(BpmnProcessNode node, CancellationToken cancellationToken = default)
        {
            // Register boundary events for the current node
            var boundaryEventNodes =
                await boundaryEventHandler.RegisterAttachedBoundaryEvents(node, Instance, cancellationToken);

            foreach (var boundaryEvent in boundaryEventNodes)
            {
                // Initially set boundary events to inactive
                boundaryEvent.DeActivate();
                Console.WriteLine($"Registered boundary event {boundaryEvent.ElementId} for node {node.ElementId}.");
            }

            try
            {
                // Process the main task if the node is executable
                if (node.IsExecutable)
                {
                    await _taskHandler.HandleAsync(node, Instance, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing node {node.ElementId}: {ex.Message}");
                node.LogException(ex.InnerException?.Message ?? ex.Message);

                // Handle error boundary events
                await boundaryEventHandler.InvokeBoundaryEventAsync(
                    node.Id,
                    Instance,
                    cancellationToken
                );
            }

            // Find and route next nodes after successful processing
            var nextNodes = await Router.FindNextNodesAsync(node, Instance);

            // Retrieve boundary nodes for the current node and add them to the next nodes
            var boundaryNodes = boundaryEventHandler.GetBoundaryNodesForNode(node.Id);
            if (boundaryNodes.Any())
            {
                foreach (var boundaryNode in boundaryNodes)
                {
                    var res = await Router.FindNextNodesAsync(boundaryNode, Instance);
                    nextNodes.AddRange(res); // Add boundary nodes to the next nodes
                }
            }

            if (nextNodes.Any())
            {
                RouteNextNodes(nextNodes);
            }

            // Finally, mark the node as expired
            node.Expire();

            StoreState(Instance);
        }


        public void StoreState(BpmnProcessInstance instance)
        {
            _processAccessor.StoreProcessState(instance.DeploymentKey, instance.Id, instance);
        }

        /// <summary>
        /// Checks if a node is an incomplete user task.
        /// </summary>
        private bool IsIncompleteUserTask(BpmnProcessNode node)
        {
            return node.UserTask != null && !node.UserTask.IsCompleted;
        }

        /// <summary>
        /// Requeues a task back into the queue.
        /// </summary>
        private void RequeueTask(BpmnProcessNode node)
        {
            Console.WriteLine($"Requeuing task: {node.ElementId}");
            _queueManager.Enqueue(node);
        }

        /// <summary>
        /// Routes the next nodes in the process.
        /// </summary>
        private void RouteNextNodes(System.Collections.Generic.List<BpmnProcessNode> nextNodes)
        {
            foreach (var nextNode in nextNodes)
            {
                _queueManager.Enqueue(nextNode);
            }
        }

        /// <summary>
        /// Waits if the process is paused. Resumes execution once the process is unpaused.
        /// </summary>
        private async Task WaitIfPaused()
        {
            await Task.Run(() =>
            {
                lock (_pauseLock)
                {
                    while (Instance.IsPaused)
                    {
                        Monitor.Wait(_pauseLock);
                    }
                }
            });
        }
    }
}