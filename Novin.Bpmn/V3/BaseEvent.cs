using Novin.Bpmn.Models;

namespace Novin.Bpmn.V3;

public abstract class BaseEvent
{
    public BpmnBoundaryEvent BoundaryEvent { get; private set; }
    public BpmnEventDefinition Event { get; private set; }
    public bool InDepended { get; protected set; }
    public bool IsTriggered { get; protected set; }
    public BpmnV3Token CurrentToken { get; private set; }

    protected BaseEvent(BpmnBoundaryEvent boundaryEvent, BpmnEventDefinition @event, BpmnV3Token currentToken)
    {
        BoundaryEvent = boundaryEvent;
        Event = @event;
        CurrentToken = currentToken;
    }

    // متد Initialize برای تنظیم اولیه ایونت
    public abstract void Initialize();

    // متد Trigger برای فعال‌سازی ایونت
    public abstract Task Trigger();
}