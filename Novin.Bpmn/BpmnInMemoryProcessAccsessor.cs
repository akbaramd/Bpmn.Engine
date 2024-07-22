using System.Collections.Concurrent;
using Novin.Bpmn.Abstractions;

namespace Novin.Bpmn;

public class BpmnInMemoryProcessAccsessor : IProcessAccsessor
{
    private readonly ConcurrentDictionary<string, BpmnProcessState?> processes = new();

    public void StoreProcessState(string processId, BpmnProcessState? processState)
    {
        processes[processId] = processState;
    }

    public BpmnProcessState? GetProcessState(string processId)
    {
        if (processes.TryGetValue(processId, out var processState))
        {
            return processState;
        }
        throw new Exception("Process state not found");
    }
}