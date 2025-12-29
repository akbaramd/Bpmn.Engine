using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public interface IBpmnModelAccessor
{
    BpmnFlowElement? GetElementById(string pid, string elementId);

    List<BpmnSequenceFlow> GetIncomingSequenceFlows(string pid, string elementId);
    List<BpmnSequenceFlow> GetOutgoingSequenceFlows(string pid, string elementId);

    List<BpmnBoundaryEvent> GetBoundaryEvents(string pid, string attachedToRef);

    BpmnProcess GetFirstProcess();

    /// <summary>
    /// Gets an error element from BPMN definitions by its ID.
    /// </summary>
    BpmnError? GetErrorElement(string errorElementId);

    List<BpmnFlowElement> GetFlowElements(string ctxBpmnProcessId);
    List<BpmnSequenceFlow> GetSequenceFlows(string ctxBpmnProcessId);
}