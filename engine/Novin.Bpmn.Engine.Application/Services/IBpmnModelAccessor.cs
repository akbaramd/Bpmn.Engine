using Novin.Bpmn.Models.Models;

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
}