using Novin.Bpmn.Models;

namespace Novin.Bpmn.Core;

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
    public BpmnProcess GetProcess(string id)
    {
        return Definitions.Items.OfType<BpmnProcess>().First(x=>x.id.Equals(id));
    }
    public BpmnStartEvent GetStartEventForProcess(string processId)
    {
        return GetProcess(processId).Items.OfType<BpmnStartEvent>().First();
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