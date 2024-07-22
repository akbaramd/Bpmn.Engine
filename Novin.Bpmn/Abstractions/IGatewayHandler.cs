namespace Novin.Bpmn.Abstractions;

public interface IGatewayHandler
{
    Task HandleGateway(BpmnProcessNode processNode, BpmnProcessEngine processEngine);
}