using System.Dynamic;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test;

public class BpmnInstance
{
    public BpmnInstance()
    {
        Id = Guid.NewGuid();
        History = new Stack<string>();
    }

    public Guid Id { get; }
    public dynamic Variables { get; } = new ExpandoObject();
    public string? CurrentNodeId { get; set; }
    public bool IsPaused { get; set; }
    public BpmnDefinitions BpmnDefinitions { get; set; } = new();
    public BpmnUserTask? PendingUserTask { get; set; }
    public Stack<string> History { get; }

    // Method to clear history if needed
    public void ClearHistory()
    {
        History.Clear();
    }
}