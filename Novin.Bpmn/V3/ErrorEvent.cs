using Novin.Bpmn.Models;

namespace Novin.Bpmn.V3;

public class ErrorEvent : BaseEvent
{


    public override void Initialize()
    {
        IsTriggered = false;
        InDepended = BoundaryEvent.cancelActivity;
    }

    public override async Task Trigger()
    {
        IsTriggered = true;
        await Task.CompletedTask; // عملیات مرتبط با ایونت
    }

    public ErrorEvent(BpmnBoundaryEvent boundaryEvent, BpmnEventDefinition @event, BpmnV3Token currentToken) : base(boundaryEvent, @event, currentToken)
    {
    }
}