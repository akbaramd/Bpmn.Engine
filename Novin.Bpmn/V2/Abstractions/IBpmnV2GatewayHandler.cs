namespace Novin.Bpmn.V2.Abstractions;

public interface IBpmnV2GatewayHandler
{
    Task<List<BpmnProcessNode>> HandleGatewayAsync(BpmnProcessNode processNode, BpmnProcessInstance instance);
}