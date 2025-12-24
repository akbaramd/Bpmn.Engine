using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

public interface ISequenceFlowSelector
{
    // برای XOR: یکی
    BpmnSequenceFlow ChooseOne(IReadOnlyList<BpmnSequenceFlow> outgoing, BpmnGateway gateway, Process process, Token token);
    IReadOnlyList<BpmnSequenceFlow> ChooseMany(IReadOnlyList<BpmnSequenceFlow> outgoing, BpmnGateway gateway, Process process, Token token);
}

