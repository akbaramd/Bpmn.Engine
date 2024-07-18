using System.Dynamic;
using Novin.Bpmn.Test.Models;
using System.Collections.Generic;
using Novin.Bpmn.Test.Process;

public class ProcessState
{
    public string Id { get; set; }
    public BpmnDefinitions Definition { get; set; }
    public dynamic Variables { get; set; } = new ExpandoObject();
    public Dictionary<string, HashSet<string>> GatewayMergeState { get; set; } = new Dictionary<string, HashSet<string>>();
    public List<ProcessNode> ActiveNodes { get; set; } = new List<ProcessNode>();
    public bool IsPaused { get; set; } = false;
    public bool IsStopped { get; set; } = false;

    public ProcessState(BpmnDefinitions definition)
    {
        Id = Guid.NewGuid().ToString();
        Definition = definition;
    }
}