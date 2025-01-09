using Novin.Bpmn.Models;

namespace Novin.Bpmn.Abstractions;

public interface ITimerHandler
{
    Task ExecuteAsync(BpmnBoundaryEvent boundaryEvent, BpmnProcessNode processNode, BpmnProcessExecutor processExecutor);
    Task CancelTimer(BpmnProcessNode processNode);
}