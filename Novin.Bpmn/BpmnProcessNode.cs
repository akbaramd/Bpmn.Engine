using Novin.Bpmn.Models;

namespace Novin.Bpmn;

public class BpmnProcessNode
{
    public string ElementId { get; set; }
    public Guid Id { get; set; }
    public bool IsExpired { get; private set; }
    public DateTime ExpiredAt { get; set; }
    public bool IsExecutable => Instances.Any(x => x.isExecutable) ;
    public bool CanBeContinue => UserTask is null || !UserTask.IsCompleted;
    public string? Details { get; set; } 
    public List<BpmnSequenceFlow> IncomingFlows { get; set; } = new List<BpmnSequenceFlow>();
    public List<BpmnSequenceFlow> OutgoingFlows { get; set; } = new List<BpmnSequenceFlow>();
    public BpmnTask? UserTask { get; private set; }
    public Stack<(string sourceElementId,Guid sourceNodeId, bool isExecutable)> Merges { get; set; } = new Stack<(string sourceElementId,Guid sourceNodeId, bool isExecutable)>(); // Merges state
    public Stack<(string sourceElementId,Guid sourceNodeId,Guid targetNodeId, bool isExecutable)> Instances { get; set; } = new Stack<(string sourceElementId,Guid sourceNodeId,Guid targetNodeId, bool isExecutable)>(); // Merges state


    public void AddUserTask(BpmnTask userTask)
    {
        UserTask = userTask;
    }
    
    public void Expire()
    {
        IsExpired = true;
        ExpiredAt = DateTime.UtcNow;
    }
    
     
}

public class BpmnTask
{
    public string DeploymentKey { get; set; }
    public string ProcessId { get; set; }
    public string TaskId { get; set; }
    public string Name { get; set; }
    public string Assignee { get; set; }
    public List<string> CandidateUsers { get; set; } = new();
    public List<string> CandidateGroups { get; set; } = new();
    public bool IsCompleted { get; set; } = false;   
}

public class BpmnUser
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Group { get; set; }
    // Additional properties as needed
}