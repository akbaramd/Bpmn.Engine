using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test;

public class BpmnRoute
{
    // Note : Branch is the sequences of Nodes in Same Path  
    // Note: Branch Can be End in EndEvent Or When reach to node to have multiple path to go (maybe: gateways)
    // Note: End Event Or Other Node Like Gateway (Have morre then one Branch) is the LastStep 

    // Id oF Element
    public string Id { get; set; }

    // BpmnElement
    public BpmnFlowElement? Element { get; set; }

    public List<BpmnSequenceFlow> Incoming { get; set; } = new List<BpmnSequenceFlow>();
    public List<BpmnSequenceFlow> Outgoing { get; set; } = new List<BpmnSequenceFlow>();
    // BPmn Branch
    public BpmnBranch Branch { get; set; }
    // Next Steps Can be have One Steps or more
    public List<BpmnRoute> NextSteps { get; set; }

    public bool Executed { get; set; }
}


