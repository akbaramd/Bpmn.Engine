using System;
using System.Collections.Generic;
using System.Linq;
using Novin.Bpmn.Models;

namespace Novin.Bpmn;

public class BpmnProcessNode
{
    public string ElementId { get; set; } // The ID of the BPMN element.
    public Guid Id { get; set; } // Unique identifier for this node instance.
    public bool IsExpired { get;  set; } // Indicates if the node has been processed and expired.
    public DateTime? ExpiredAt { get;  set; } // Timestamp when the node was marked as expired.
    public bool IsExecutable
    {
        get
        {
            // Only executable if not expired, instances are executable, and it is interruptible.
            return  Instances.Any(x => x.isExecutable);
        }
    } // Determines if the node has executable instances.
    public bool CanBeContinue => ( UserTask == null || UserTask.IsCompleted); // Checks if the node can proceed further.
    public string? Details { get; set; } // Additional details or metadata about the node.
    public List<BpmnSequenceFlow> IncomingFlows { get; set; } = new(); // Incoming sequence flows to this node.
    public List<BpmnSequenceFlow> OutgoingFlows { get; set; } = new(); // Outgoing sequence flows from this node.
    public BpmnTask? UserTask { get;  set; } // User task associated with this node, if any.
    public bool IsInterruptible { get; private set; } = true;
    // Stores exceptions encountered during the node's execution.
    public List<string> Exceptions { get; set; } = new();

    // Tracks merges that occurred during node processing.
    public Stack<(string sourceElementId, Guid sourceNodeId, bool isExecutable)> Merges { get; set; } = new();

    // Tracks instances of the node for execution.
    public Stack<(string sourceElementId, Guid sourceNodeId, Guid targetNodeId, bool isExecutable)> Instances { get; set; } = new();

    public BpmnProcessNode(string elementId, Guid id, List<BpmnSequenceFlow> incomingFlows, List<BpmnSequenceFlow> outgoingFlows)
    {
        ElementId = elementId;
        Id = id;
        IncomingFlows = incomingFlows;
        OutgoingFlows = outgoingFlows;
    }

    public void AddUserTask(BpmnTask userTask)
    {
        if (UserTask != null)
        {
            throw new InvalidOperationException("A user task is already assigned to this node.");
        }
        UserTask = userTask;
    }
    public void SetInterruptible(bool isInterruptible)
    {
        IsInterruptible = isInterruptible;
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

    public void AddIncomingFlows(IEnumerable<BpmnSequenceFlow> flows)
    {
        IncomingFlows.AddRange(flows);
    }

    public void AddOutgoingFlow(BpmnSequenceFlow flow)
    {
        OutgoingFlows.Add(flow);
    }

    public void AddOutgoingFlows(IEnumerable<BpmnSequenceFlow> flows)
    {
        OutgoingFlows.AddRange(flows);
    }

    public void AddInstance(string sourceElementId, Guid sourceNodeId, Guid targetNodeId, bool isExecutable)
    {
        // Check if an instance with the same sourceNodeId and targetNodeId already exists
        var existingInstance = Instances.FirstOrDefault(instance => 
            instance.sourceNodeId == sourceNodeId && instance.targetNodeId == targetNodeId);

        if (existingInstance != default)
        {
            // Update the existing instance's properties
            Instances = new Stack<(string sourceElementId, Guid sourceNodeId, Guid targetNodeId, bool isExecutable)>(
                Instances.Select(instance =>
                    instance.Equals(existingInstance)
                        ? (existingInstance.sourceElementId, existingInstance.sourceNodeId, targetNodeId, isExecutable)
                        : instance)
            );
        }
        else
        {
            // Add a new instance if none exists
            Instances.Push((sourceElementId, sourceNodeId, targetNodeId, isExecutable));
        }
    }

    public void AddMerge(string sourceElementId, Guid sourceNodeId, bool isExecutable)
    {
        Merges.Push((sourceElementId, sourceNodeId, isExecutable));
    }

    public void LogException(string exceptionMessage)
    {
        if (string.IsNullOrWhiteSpace(exceptionMessage))
        {
            throw new ArgumentException("Exception message cannot be null or empty.", nameof(exceptionMessage));
        }
        Exceptions.Add(exceptionMessage);
    }

    public override string ToString()
    {
        return $"Node: {ElementId}, ID: {Id}, Executable: {IsExecutable}, Expired: {IsExpired}";
    }
}

public class BpmnTask
{
    public string DeploymentKey { get; set; } // Deployment key associated with the task.
    public Guid ProcessId { get; set; } // ID of the process instance the task belongs to.
    public Guid TaskId { get; set; } // Unique identifier for the task.
    public string FormId { get; set; } // ID of the form associated with the task.
    public string Name { get; set; } // Task name.
    public string Assignee { get; private set; } // User assigned to this task.
    public List<string> CandidateUsers { get; set; } = new(); // List of users who can claim the task.
    public List<string> CandidateGroups { get; set; } = new(); // List of groups who can claim the task.
    public bool IsCompleted { get; private set; } = false; // Indicates if the task is completed.

    public BpmnTask(Guid taskId, string formId, string name, string assignee, Guid processId, string deploymentKey,bool isCompleted)
    {
        TaskId = taskId;
        FormId = formId;
        Name = name;
        Assignee = assignee;
        ProcessId = processId;
        DeploymentKey = deploymentKey;
        this.IsCompleted = isCompleted;
    }

    public void CompleteTask()
    {
        if (IsCompleted)
        {
            throw new InvalidOperationException("Task is already completed.");
        }
        IsCompleted = true;
    }

    public void AddCandidateUser(string user)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            throw new ArgumentException("Candidate user cannot be null or empty.", nameof(user));
        }
        CandidateUsers.Add(user);
    }

    public void AddCandidateGroup(string group)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            throw new ArgumentException("Candidate group cannot be null or empty.", nameof(group));
        }
        CandidateGroups.Add(group);
    }

    public void AddCandidateUsers(IEnumerable<string> users)
    {
        if (users == null || !users.Any())
        {
            throw new ArgumentException("Candidate users cannot be null or empty.", nameof(users));
        }
        CandidateUsers.AddRange(users);
    }

    public void AddCandidateGroups(IEnumerable<string> groups)
    {
        if (groups == null || !groups.Any())
        {
            throw new ArgumentException("Candidate groups cannot be null or empty.", nameof(groups));
        }
        CandidateGroups.AddRange(groups);
    }

    public void SetAssignee(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("Assignee cannot be null or empty.", nameof(userId));
        }
        Assignee = userId;
    }

    public override string ToString()
    {
        return $"Task: {Name}, ID: {TaskId}, Assignee: {Assignee}, Completed: {IsCompleted}";
    }
}

public class BpmnUser
{
    public string Id { get; set; } // User ID.
    public string Name { get; set; } // User name.
    public string Group { get; set; } // User's group affiliation.

    public override string ToString()
    {
        return $"User: {Name}, ID: {Id}, Group: {Group}";
    }
}
