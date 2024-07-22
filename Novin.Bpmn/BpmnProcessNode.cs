using Novin.Bpmn.Models;

namespace Novin.Bpmn;

public class BpmnProcessNode
{
    public string ElementId { get; set; }
    public Guid Id { get; set; }
    public bool IsExpired { get; set; } = false;
    public DateTime Timestamp { get; set; }
    public bool IsExecutable => Instances.Any(x => x.isExecutable) ;
    public bool CanBeContinue { get; set; } = true;
    public string? Details { get; set; } 
    // New properties for user handling
    public string? AssignedUserId { get; set; }
    public string? CompletedByUserId { get; set; }
    public List<BpmnSequenceFlow> IncomingFlows { get; set; } = new List<BpmnSequenceFlow>();
    public List<BpmnSequenceFlow> OutgoingFlows { get; set; } = new List<BpmnSequenceFlow>();

    public Stack<(string sourceElementId,Guid sourceNodeId, bool isExecutable)> Merges { get; set; } = new Stack<(string sourceElementId,Guid sourceNodeId, bool isExecutable)>(); // Merges state
    public Stack<(string sourceElementId,Guid sourceNodeId,Guid targetNodeId, bool isExecutable)> Instances { get; set; } = new Stack<(string sourceElementId,Guid sourceNodeId,Guid targetNodeId, bool isExecutable)>(); // Merges state

     
}

public class BpmnTask
{
    public string TaskId { get; set; }
    public string Name { get; set; }
    public string Assignee { get; set; }
    public List<string> CandidateUsers { get; set; } = new();
    public List<string> CandidateGroups { get; set; } = new();
    public bool Status { get; set; } = false;   
}

public class BpmnUser
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Group { get; set; }
    // Additional properties as needed
}