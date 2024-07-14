using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test.Executors.Abstracts;

public interface IGatewayExecutor
{
    BpmnFlowElement? Execute(BpmnFlowElement element, BpmnInstance? context);
}