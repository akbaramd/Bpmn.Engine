using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public enum ElementProcessResult
{
    Completed,   // element done, should navigate (normal case)
    Waiting,     // element paused (userTask, join waiting, etc.)
    Consumed,    // token consumed/replaced (split that removed parent, etc.)
    Terminated,  // token terminated
    Failed       // token failed
    ,
    NoOp
}

public interface IBpmnElementHandler
{
    bool CanHandle(BpmnFlowElement element);

    Task<ElementProcessResult> ProcessAsync(
        Process process,
        Token token,
        NodeInstance nodeInstance,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct);

    Task NavigateAsync(
        Process process,
        Token token,
        NodeInstance nodeInstance,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct);
}
