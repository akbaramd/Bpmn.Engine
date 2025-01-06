namespace Novin.Bpmn.Abstractions;

public interface IGatewayHandler
{
    Task HandleGateway(BpmnProcessNode processNode, BpmnProcessExecutor processExecutor);
}