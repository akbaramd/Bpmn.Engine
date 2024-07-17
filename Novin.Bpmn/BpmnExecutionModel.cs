using Novin.Bpmn.Test.Models;

public class BpmnExecutionModel
{
    public List<BpmnNode> ActiveRoutes { get; private set; } = new List<BpmnNode>();
    public Stack<BpmnNode> History { get; private set; } = new Stack<BpmnNode>();
    public Dictionary<string, List<string>> GatewayBranches { get; private set; } = new Dictionary<string, List<string>>();
    public List<BpmnUserTask> PendingUserTasks { get; private set; } = new List<BpmnUserTask>();

    public void AddActiveRoute(BpmnNode node)
    {
        ActiveRoutes.Add(node);
    }

    public void AddActiveRoutes(IEnumerable<BpmnNode> nodes)
    {
        ActiveRoutes.AddRange(nodes);
    }

    public void RemoveActiveRoute(BpmnNode node)
    {
        ActiveRoutes.Remove(node);
    }

    public bool HasActiveRoutes()
    {
        return ActiveRoutes.Any();
    }

    public IEnumerable<BpmnNode> GetActiveRoutes()
    {
        return ActiveRoutes.Where(route => !route.Executed);
    }

    public void AddToHistory(BpmnNode node)
    {
        History.Push(node);
    }

    public bool HasPendingUserTasks()
    {
        return PendingUserTasks.Any();
    }
}