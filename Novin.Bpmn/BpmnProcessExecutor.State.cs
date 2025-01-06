using System;
using System.Linq;
using System.Threading.Tasks;
using Novin.Bpmn.Models;

namespace Novin.Bpmn
{
    public partial class BpmnProcessExecutor
    {
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
    }
}