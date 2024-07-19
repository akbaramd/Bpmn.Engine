using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test;

public class BpmnFakeNode : BpmnNode
{
    public static string FakeToken = "FakeToken";

    public BpmnFakeNode(BpmnFlowElement element)
    {
        Id = element.id;
        Token = FakeToken;
        Element = element;
    }
}