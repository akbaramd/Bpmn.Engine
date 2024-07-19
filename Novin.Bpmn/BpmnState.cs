using System.Dynamic;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test;

public class ProcessState
{
    public string Id { get; set; }
    public BpmnDefinitions Definition { get; set; }
    public dynamic Variables { get; set; } = new ExpandoObject();

    public Dictionary<string, Stack<string>> GatewayMergeState { get; set; } =
        new Dictionary<string, Stack<string>>();

    public List<BpmnNode> ActiveNodes { get; set; } = new List<BpmnNode>();
    public bool IsPaused { get; set; } = false;
    public bool IsStopped { get; set; } = false;

    public ProcessState(BpmnDefinitions definition)
    {
        Id = Guid.NewGuid().ToString();
        Definition = definition;
    }
}