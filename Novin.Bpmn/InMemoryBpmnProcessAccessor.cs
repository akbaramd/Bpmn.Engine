using System.Collections.Concurrent;
using Novin.Bpmn.Abstractions;

namespace Novin.Bpmn;

public class InMemoryBpmnProcessAccessor : IBpmnProcessAccessor
{
    private readonly ConcurrentDictionary<Guid, BpmnProcessInstance?> processes = new();

    public void StoreProcessState(string deploymentKey,Guid processId, BpmnProcessInstance? processState)
    {
        processes[processId] = processState;
    }

    public BpmnProcessInstance? GetProcessState(Guid processId)
    {
        if (processes.TryGetValue(processId, out var processState))
        {
            return processState;
        }
        throw new Exception("Process state not found");
    }
}