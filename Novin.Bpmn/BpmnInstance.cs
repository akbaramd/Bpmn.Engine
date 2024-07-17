using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Novin.Bpmn.Test;
using Novin.Bpmn.Test.Models;

public class BpmnInstance
{
    public BpmnDefinitions Definitions { get; }
    public  Stack<BpmnBranch> _activeBranch;
    private readonly List<BpmnUserTask> _pendingUserTasks;
    public dynamic Variables { get; set; }

    public BpmnInstance(BpmnDefinitions definitions)
    {
        Definitions = definitions;
        _activeBranch = new Stack<BpmnBranch>();
        _pendingUserTasks = new List<BpmnUserTask>();
        Variables = new ExpandoObject();
    }

  
  


    public bool HasPendingUserTasks()
    {
        return _pendingUserTasks.Count > 0;
    }

    public void AddPendingUserTask(BpmnUserTask userTask)
    {
        _pendingUserTasks.Add(userTask);
    }

    public void RemovePendingUserTask(BpmnUserTask userTask)
    {
        _pendingUserTasks.Remove(userTask);
    }

 
}
