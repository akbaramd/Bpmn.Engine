using System.Collections.Concurrent;
using System.Dynamic;
using System.Text.Json;
using Novin.Bpmn.Models;

namespace Novin.Bpmn;

public class BpmnProcessState
{
    public string Id { get; set; }
    public string ProcessElementId { get;  }
    public BpmnDefinitions Definition { get; set; }
    public dynamic Variables { get; set; } = new ExpandoObject();
    public Queue<BpmnProcessNode> NodeQueue { get; set; } = new();
    public Stack<BpmnProcessNode> NodeStack { get; set; } = new();
    public Stack<BpmnNodeTransition> TransitionStack { get; set; } = new();
    public Stack<string> Exceptions { get; set; } = new();
    public bool IsPaused { get; set; } = false;
    public bool IsStopped { get; set; } = false;
    public bool IsFinished { get; set; } = false;

    public ConcurrentDictionary<string, BpmnProcessNode> WaitingUserTasks { get; private set; }

    public BpmnProcessState(BpmnDefinitions definitions , string processElementId)
    {
        Id = Guid.NewGuid().ToString();
        Definition = definitions;
        WaitingUserTasks = new ConcurrentDictionary<string, BpmnProcessNode>();
        ProcessElementId = processElementId;
    }

    // Method to serialize the state
    public static BpmnProcessState RestoreState(string savedState, BpmnDefinitions definitions)
    {
        var state = JsonSerializer.Deserialize<BpmnProcessState>(savedState);
        state.WaitingUserTasks = new ConcurrentDictionary<string, BpmnProcessNode>(state.WaitingUserTasks);
        return state;
    }

    public string SaveState()
    {
        return JsonSerializer.Serialize(this);
    }
}