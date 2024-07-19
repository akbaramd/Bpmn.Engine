using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test.Core;

public class BpmnDefinitionsHandler
{
    public BpmnDefinitions Definitions { get; }

    public BpmnDefinitionsHandler(BpmnDefinitions definitions)
    {
        Definitions = definitions;
    }

    public BpmnProcess GetFirstProcess()
    {
        return Definitions.Items.OfType<BpmnProcess>().First();
    }

    public BpmnStartEvent GetFirstStartEvent()
    {
        return GetFirstProcess().Items.OfType<BpmnStartEvent>().First();
    }

    public BpmnFlowElement GetElementById(string id)
    {
        return GetFirstProcess().Items.First(x => x.id.Equals(id));
    }

    public List<BpmnSequenceFlow> GetOutgoingSequenceFlows(BpmnFlowElement flowElement)
    {
        return GetFirstProcess().Items.OfType<BpmnSequenceFlow>().Where(x => x.sourceRef.Equals(flowElement.id))
            .ToList();
    }

    public List<BpmnSequenceFlow> GetIncomingSequenceFlows(BpmnFlowElement flowElement)
    {
        return GetFirstProcess().Items.OfType<BpmnSequenceFlow>().Where(x => x.targetRef.Equals(flowElement.id))
            .ToList();
    }
}