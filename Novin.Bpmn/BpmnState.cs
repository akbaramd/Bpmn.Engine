using System.Collections.Concurrent;
using System.Dynamic;
using System.Text.Json;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test;

public class ProcessState
{
    public string Id { get; set; }
    public BpmnDefinitions Definition { get; set; }
    public dynamic Variables { get; set; } = new ExpandoObject();

    public Dictionary<string, Stack<string>> GatewayMergeState { get; set; } =
        new Dictionary<string, Stack<string>>();

    public ConcurrentBag<BpmnNode> ActiveNodes { get; set; } = new ConcurrentBag<BpmnNode>();
    public bool IsPaused { get; set; } = false;
    public bool IsStopped { get; set; } = false;

    public ConcurrentDictionary<string, BpmnNode> WaitingUserTasks { get; private set; }
    public ProcessState(BpmnDefinitions definitions)
    {
        Definition = definitions;
        WaitingUserTasks = new ConcurrentDictionary<string, BpmnNode>();
    }

    // Method to serialize the state
    public static ProcessState RestoreState(string savedState, BpmnDefinitions definitions)
    {
        // Restore existing state...

        var state = JsonSerializer.Deserialize<ProcessState>(savedState);
        state.WaitingUserTasks = new ConcurrentDictionary<string, BpmnNode>(state.WaitingUserTasks);
        return state;
        return state;
    }

    public string SaveState()
    {
        // Save existing state...

        return JsonSerializer.Serialize(this);
    }
}