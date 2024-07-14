using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test.Executors.Abstracts;

public interface ITaskExecutor
{
    Task ExecuteAsync(BpmnFlowElement element, BpmnInstance? context);
}