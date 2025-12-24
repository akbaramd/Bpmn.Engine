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

    public BpmnProcess GetFirstProcess()
        => _defs.GetFirstProcess();
}