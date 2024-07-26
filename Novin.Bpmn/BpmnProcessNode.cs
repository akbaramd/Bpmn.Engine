using Novin.Bpmn.Models;

namespace Novin.Bpmn;

public class BpmnProcessNode
{
    public string ElementId { get; set; }
    public Guid Id { get; set; }
    public bool IsExpired { get;  set; }
    public DateTime ExpiredAt { get;  set; }
    public bool IsExecutable => Instances.Any(x => x.isExecutable);
    public bool CanBeContinue => UserTask == null || UserTask.IsCompleted;
    public string? Details { get; set; }
    public List<BpmnSequenceFlow> IncomingFlows { get;  set; } = new();
    public List<BpmnSequenceFlow> OutgoingFlows { get;  set; } = new();
    public BpmnTask? UserTask { get; set; }

    public Stack<(string sourceElementId, Guid sourceNodeId, bool isExecutable)> Merges { get; set; } =
        new();

    public Stack<(string sourceElementId, Guid sourceNodeId, Guid targetNodeId, bool isExecutable)> Instances
    {
        get;
        set;
    } = new();

    public BpmnProcessNode(string elementId, Guid id,List<BpmnSequenceFlow> getIncomingSequenceFlows, List<BpmnSequenceFlow> getOutgoingSequenceFlows)
    {
        ElementId = elementId;
        Id = id;
        OutgoingFlows = getOutgoingSequenceFlows;
        IncomingFlows = getIncomingSequenceFlows;
    }

    public void AddUserTask(BpmnTask userTask)
    {
        UserTask = userTask;
    }

    public void Expire()
    {
        IsExpired = true;
        ExpiredAt = DateTime.UtcNow;
    }

    public void AddIncomingFlow(BpmnSequenceFlow flow)
    {
        IncomingFlows.Add(flow);
    }
    public void AddIncomingFlows(List<BpmnSequenceFlow> flow)
    {
        IncomingFlows.AddRange(flow);
    }
    public void AddOutgoingFlow(BpmnSequenceFlow flow)
    {
        OutgoingFlows.Add(flow);
    }
    public void AddOutgoingFlow(List<BpmnSequenceFlow> flow)
    {
        OutgoingFlows.AddRange(flow);
    }
    public void AddInstance(string sourceElementId, Guid sourceNodeId, Guid targetNodeId, bool isExecutable)
    {
        Instances.Push((sourceElementId, sourceNodeId, targetNodeId, isExecutable));
    }

    public void AddMerge(string sourceElementId, Guid sourceNodeId, bool isExecutable)
    {
        Merges.Push((sourceElementId, sourceNodeId, isExecutable));
    }
}

public class BpmnTask
{
    public string DeploymentKey { get;  set; }
    public Guid ProcessId { get;  set; }
    public Guid TaskId { get;  set; }
    public string Name { get;  set; }
    public string Assignee { get;  set; }
    public string FormId { get;  set; }
    public List<string> CandidateUsers { get;  set; }
    public List<string> CandidateGroups { get;  set; }
    public bool IsCompleted { get; set; } = false;

    public BpmnTask(Guid taskId,string formId, string name, string assignee, Guid processId, string deploymentKey)
    {
        TaskId = taskId;
        Name = name;
        Assignee = assignee;
        FormId = formId;
        ProcessId = processId;
        DeploymentKey = deploymentKey;
        CandidateUsers = new List<string>();
        CandidateGroups = new List<string>();
    }

    public void CompleteTask()
    {
        IsCompleted = true;
    }

    public void AddCandidateUser(string user)
    {
        CandidateUsers.Add(user);
    }

    public void AddCandidateGroup(string group)
    {
        CandidateGroups.Add(group);
    }

    public void AddCandidateUsers(IEnumerable<string> users)
    {
        CandidateUsers.AddRange(users);
    }

    public void AddCandidateGroups(IEnumerable<string> groups)
    {
        CandidateGroups.AddRange(groups);
    }

    public void SetAssignee(string userId)
    {
        Assignee = userId;
    }
}
public class BpmnUser
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Group { get; set; }
    // Additional properties as needed
}