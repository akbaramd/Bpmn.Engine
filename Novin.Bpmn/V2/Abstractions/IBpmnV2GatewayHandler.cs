namespace Novin.Bpmn.Abstractions;

public interface IBpmnV2GatewayHandler
{
    Task<List<BpmnProcessNode>> HandleGatewayAsync(BpmnProcessNode processNode, BpmnProcessInstance instance);
}