namespace Novin.Bpmn.Test.Process;

public interface IGatewayHandler
{
    Task HandleGateway(ProcessNode node, ProcessEngine engine);
}