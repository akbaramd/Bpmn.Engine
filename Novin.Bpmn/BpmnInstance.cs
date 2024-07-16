using System.Dynamic;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test;

public class BpmnInstance
{
    public List<BpmnRoute> ActiveRoutes = new List<BpmnRoute>();

    public BpmnInstance()
    {
        Id = Guid.NewGuid();
        History = new Stack<BpmnRoute>();
    }

    public Guid Id { get; }
    public dynamic Variables { get; } = new ExpandoObject();
    public bool IsPaused { get; set; }
    public BpmnUserTask? PendingUserTask { get; set; }
    public Stack<BpmnRoute> History { get; }

    // Method to clear history if needed
    public void ClearHistory()
    {
        History.Clear();
    }
}