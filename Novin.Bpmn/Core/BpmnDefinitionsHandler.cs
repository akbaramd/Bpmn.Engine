using Novin.Bpmn.Models;

namespace Novin.Bpmn.Core;

public class BpmnDefinitionsHandler
{
    public BpmnDefinitions Definitions { get; }

    public BpmnDefinitionsHandler(BpmnDefinitions definitions)
    {
        Definitions = definitions;
    }
    public BpmnDefinitionsHandler(string definitions)
    {
        Definitions = BpmnDefinitionSerializer.Deserialize(
            definitions);
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
    
    public List<BpmnStartEvent> GetStartEventsForProcess(string processId)
    {
        return GetProcess(processId).Items.OfType<BpmnStartEvent>().ToList();
    }
    public BpmnEndEvent GetEndEventForProcess(string processId)
    {
        return GetProcess(processId).Items.OfType<BpmnEndEvent>().First();
    }

    public List<BpmnBoundaryEvent> GetAttachedEvents(string refId)
    {
        return GetFirstProcess().Items.OfType<BpmnBoundaryEvent>().Where(x=>x.attachedToRef.Name == refId).ToList();
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