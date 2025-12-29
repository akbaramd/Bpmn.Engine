using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class BpmnModelAccessor : IBpmnModelAccessor
{
    private readonly BpmnDefinitionsService _defs;

    public BpmnModelAccessor(BpmnDefinitionsService defs)
        => _defs = defs ?? throw new ArgumentNullException(nameof(defs));

    public BpmnFlowElement? GetElementById(string pid, string elementId)
        => _defs.GetElementById(pid, elementId);

    public List<BpmnSequenceFlow> GetIncomingSequenceFlows(string pid, string elementId)
        => _defs.GetIncomingSequenceFlows(pid, elementId);

    public List<BpmnSequenceFlow> GetOutgoingSequenceFlows(string pid, string elementId)
        => _defs.GetOutgoingSequenceFlows(pid, elementId);

    public List<BpmnBoundaryEvent> GetBoundaryEvents(string pid, string attachedToRef)
    {
        var result = _defs.GetBoundaryEvents(pid, attachedToRef);
        return result;
    }

    public BpmnProcess GetFirstProcess()
        => _defs.GetFirstProcess();

    public BpmnError? GetErrorElement(string errorElementId)
        => _defs.GetErrorElement(errorElementId);

    public List<BpmnFlowElement> GetFlowElements(string ctxBpmnProcessId)
    {
        return _defs.GetFlowElements(ctxBpmnProcessId);
    }

    public List<BpmnSequenceFlow> GetSequenceFlows(string ctxBpmnProcessId)
    {
        return _defs.GetSequenceFlows(ctxBpmnProcessId);
    }
}