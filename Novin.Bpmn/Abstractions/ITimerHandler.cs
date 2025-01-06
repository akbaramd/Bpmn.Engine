using Novin.Bpmn;
using Novin.Bpmn.Models;

public interface ITimerHandler
{
    Task ExecuteAsync(BpmnBoundaryEvent boundaryEvent, BpmnProcessNode processNode, BpmnProcessExecutor processExecutor);
    Task CancelTimer(BpmnProcessNode processNode);
}