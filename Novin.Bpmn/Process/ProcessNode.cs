using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test.Process;

public class ProcessNode
{
    public string Id { get; set; }
    public BpmnFlowElement  Element { get; set; }
    public string Token { get; set; }
    
}