namespace Novin.Bpmn.V2;

public class BpmnQueueManager
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly BpmnProcessInstance _bpmnProcessInstance;

    public BpmnQueueManager(BpmnProcessInstance bpmnProcessInstance)
    {
        _bpmnProcessInstance = bpmnProcessInstance;
    }

    public void Enqueue(BpmnProcessNode node)
    {
        _lock.EnterWriteLock();
        try
        {
            _bpmnProcessInstance.NextQueue.Enqueue(node);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public BpmnProcessNode Dequeue()
    {
        _lock.EnterWriteLock();
        try
        {
            return _bpmnProcessInstance.NextQueue.Dequeue();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public BpmnProcessNode Peek()
    {
        _lock.EnterReadLock();
        try
        {
            return _bpmnProcessInstance.NextQueue.Peek();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
    public void EnqueueStartNodes()
    {
        var startEvents = _bpmnProcessInstance.DefinitionsHandler.GetStartEventsForProcess(_bpmnProcessInstance.ProcessElementId);

        if (startEvents == null || !startEvents.Any())
        {
            throw new InvalidOperationException($"No start events found for process {_bpmnProcessInstance.ProcessElementId}.");
        }

        foreach (var startEvent in startEvents)
        {
            var startNode = _bpmnProcessInstance.GetOrCreateNode(startEvent.id, true, null, null);
            Enqueue(startNode);
        }
    }
    public int Count => _bpmnProcessInstance.NextQueue.Count;
}