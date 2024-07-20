namespace Novin.Bpmn.Abstractions;

public interface IGatewayHandler
{
    Task HandleGateway(BpmnNode node, BpmnEngine engine);
}