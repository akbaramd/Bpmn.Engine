using System.Collections.Concurrent;
using System.Dynamic;
using System.Text.Json;
using Novin.Bpmn.Test;
using Novin.Bpmn.Test.Models;

public class ProcessState
{
    public string Id { get; set; }
    public BpmnDefinitions Definition { get; set; }
    public dynamic Variables { get; set; } = new ExpandoObject();

    public Dictionary<string, BpmnNode> Nodes { get; set; } = new();
    public ConcurrentBag<BpmnNode> ActiveNodes { get; set; } = new();
    public bool IsPaused { get; set; } = false;
    public bool IsStopped { get; set; } = false;

    public ConcurrentDictionary<string, BpmnNode> WaitingUserTasks { get; private set; }

    public ProcessState(BpmnDefinitions definitions)
    {
        Id = Guid.NewGuid().ToString();
        Definition = definitions;
        WaitingUserTasks = new ConcurrentDictionary<string, BpmnNode>();
    }

    // Method to serialize the state
    public static ProcessState RestoreState(string savedState, BpmnDefinitions definitions)
    {
        var state = JsonSerializer.Deserialize<ProcessState>(savedState);
        state.WaitingUserTasks = new ConcurrentDictionary<string, BpmnNode>(state.WaitingUserTasks);
        return state;
    }

    public string SaveState()
    {
        return JsonSerializer.Serialize(this);
    }
}