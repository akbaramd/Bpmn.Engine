using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// نتیجه‌ی پردازش مرحله‌ی "Token-level" (قبل/جدا از NodeInstance).
/// این مرحله برای گیت‌هایی مثل Join، Event subscription checks، و ... است.
/// </summary>

public enum TokenProcessResult
{
    Continue,    // token can proceed to NodeProcessAsync
    Waiting,     // token paused (join waiting, userTask waiting, etc.)
    Consumed,    // token was consumed/merged/late-arrival (should not run node process)
    Failed,      // token failed
    Terminated,  // token terminated
    NoOp         // nothing to do
}

/// <summary>
/// نتیجه‌ی پردازش NodeInstance (یعنی اجرای خود Element).
/// </summary>
public enum ElementProcessResult
{
    Completed,   // element done, should navigate
    Waiting,     // element paused
    Consumed,    // token consumed/replaced
    Terminated,  // token terminated
    Failed,      // token failed
    NoOp
}

public interface IBpmnElementHandler
{
    bool CanHandle(BpmnFlowElement element);

    /// <summary>
    /// Token-level phase (بدون NodeInstance): تصمیم برای اینکه آیا این توکن همین الان اجازه‌ی ورود به اجرای نود را دارد یا نه.
    /// مثال: Join gateway اگر هنوز همه نرسیدن => Waiting
    /// </summary>
    Task<TokenProcessResult> TokenProcessAsync(
        Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct);

    /// <summary>
    /// Node-level phase: اجرای واقعی element با داشتن NodeInstance
    /// </summary>
    Task<ElementProcessResult> NodeProcessAsync(
        Process process,
        Token token,
        NodeInstance nodeInstance,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct);

    /// <summary>
    /// Navigation phase (move/split/route)
    /// </summary>
    Task TokenNavigateAsync(
        Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct);
}
