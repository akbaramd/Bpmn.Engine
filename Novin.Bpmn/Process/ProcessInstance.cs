using System.Dynamic;
using Novin.Bpmn.Test.Models;
using Novin.Bpmn.Test.Process;

public class ProcessInstance
{
    public string Id { get; set; }
    public BpmnDefinitions Definition { get; set; }
    public dynamic Variables { get; set; } = new ExpandoObject();
    public List<ProcessNode> ActiveNode { get; set; } = new List<ProcessNode>();
    public Dictionary<string, HashSet<string>> GatewayMergeState { get; set; } = new Dictionary<string, HashSet<string>>();
    public Dictionary<string, HashSet<string>> GatewayForkState { get; set; } = new Dictionary<string, HashSet<string>>();
}