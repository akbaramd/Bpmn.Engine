using System.Dynamic;
using Novin.Bpmn.Test.Models;

public class ProcessInstance
{
    public string Id { get; set; }
    public BpmnDefinitions Definition { get; set; }
    public dynamic Variables { get; set; } = new ExpandoObject();
    public List<BpmnFlowElement> ActiveTasks { get; set; } = new List<BpmnFlowElement>();
    public Dictionary<string, int> GatewayState { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, HashSet<string>> ForkIds { get; set; } = new Dictionary<string, HashSet<string>>();
}