using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test;

public class BpmnNode
{
    public string Id { get; set; }
    public BpmnFlowElement Element { get; set; }
    public string Token { get; set; }
}