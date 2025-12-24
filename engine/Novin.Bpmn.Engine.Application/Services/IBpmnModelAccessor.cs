using Novin.Bpmn.Models.Models;

public interface IBpmnModelAccessor
{
    BpmnFlowElement? GetElementById(string pid, string elementId);

    List<BpmnSequenceFlow> GetIncomingSequenceFlows(string pid, string elementId);
    List<BpmnSequenceFlow> GetOutgoingSequenceFlows(string pid, string elementId);

    BpmnProcess GetFirstProcess();
}