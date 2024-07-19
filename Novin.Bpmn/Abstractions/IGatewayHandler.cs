namespace Novin.Bpmn.Test.Abstractions;

public interface IGatewayHandler
{
    Task HandleGateway(BpmnNode node, BpmnEngine engine);
}