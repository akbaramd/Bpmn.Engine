using System.Collections.Concurrent;
using System.Dynamic;
using System.Text.Json;
using Novin.Bpmn.Models;

namespace Novin.Bpmn;

public class BpmnState
{
    public string Id { get; set; }
    public string ProcessId { get;  }
    public BpmnDefinitions Definition { get; set; }
    public dynamic Variables { get; set; } = new ExpandoObject();
    public Queue<BpmnNode> NodeQueue { get; set; } = new();
    public Stack<BpmnNode> NodeStack { get; set; } = new();
    public Stack<string> Exceptions { get; set; } = new();
    public bool IsPaused { get; set; } = false;
    public bool IsStopped { get; set; } = false;

    public ConcurrentDictionary<string, BpmnNode> WaitingUserTasks { get; private set; }

    public BpmnState(BpmnDefinitions definitions , string processId)
    {
        Id = Guid.NewGuid().ToString();
        Definition = definitions;
        WaitingUserTasks = new ConcurrentDictionary<string, BpmnNode>();
        ProcessId = processId;
    }

    // Method to serialize the state
    public static BpmnState RestoreState(string savedState, BpmnDefinitions definitions)
    {
        var state = JsonSerializer.Deserialize<BpmnState>(savedState);
        state.WaitingUserTasks = new ConcurrentDictionary<string, BpmnNode>(state.WaitingUserTasks);
        return state;
    }

    public string SaveState()
    {
        return JsonSerializer.Serialize(this);
    }
}