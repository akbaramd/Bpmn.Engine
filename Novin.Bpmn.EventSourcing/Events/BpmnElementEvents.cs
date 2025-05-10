using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Collections.Generic;

namespace Novin.Bpmn.EventSourcing.Events;

/// <summary>
/// رویداد المان BPMN
/// </summary>
public abstract record ElementEvent : BpmnEvent
{
    /// <summary>
    /// شناسه المان BPMN
    /// </summary>
    public required string ElementId { get; init; }
    
    /// <summary>
    /// نوع المان BPMN
    /// </summary>
    public required string ElementType { get; init; }
}

/// <summary>
/// رویداد فعال‌سازی المان BPMN
/// این رویداد معادل بخش ورود یک جریان اجرا به المان است
/// </summary>
public record ElementActivating : ElementEvent
{
    /// <summary>
    /// شناسه المان قبلی که جریان از آن آمده (در صورت وجود)
    /// </summary>
    public string? SourceElementId { get; init; }
    
    /// <summary>
    /// شناسه جریان (Sequence Flow) که از آن عبور کرده (در صورت وجود)
    /// </summary>
    public string? SequenceFlowId { get; init; }
    
    /// <inheritdoc/>
    public new string Intent { get; init; } = "ACTIVATING";
}

/// <summary>
/// رویداد فعال شدن المان BPMN
/// </summary>
public record ElementActivated : ElementEvent
{
    /// <inheritdoc/>
    public new string Intent { get; init; } = "ACTIVATED";
}

/// <summary>
/// رویداد درحال تکمیل المان BPMN
/// </summary>
public record ElementCompleting : ElementEvent
{
    /// <summary>
    /// مسیرهای خروجی فعال که جریان باید به آنها ادامه یابد
    /// </summary>
    public ICollection<string>? OutgoingFlowIds { get; init; }
    
    /// <summary>
    /// خروجی المان (در صورت وجود)
    /// </summary>
    public object? Output { get; init; }
    
    /// <summary>
    /// متغیرهای بروزرسانی شده (در صورت وجود)
    /// </summary>
    public Dictionary<string, object>? UpdatedVariables { get; init; }
    
    /// <inheritdoc/>
    public new string Intent { get; init; } = "COMPLETING";
}

/// <summary>
/// رویداد تکمیل المان BPMN
/// </summary>
public record ElementCompleted : ElementEvent
{
    /// <inheritdoc/>
    public new string Intent { get; init; } = "COMPLETED";
}

/// <summary>
/// رویداد شکست المان BPMN
/// </summary>
public record ElementFailed : ElementEvent
{
    /// <summary>
    /// کد خطا
    /// </summary>
    public string? ErrorCode { get; init; }
    
    /// <summary>
    /// پیام خطا
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// آیا این خطا توسط رویداد مرزی مدیریت می‌شود
    /// </summary>
    public bool HasErrorBoundaryEvent { get; init; }
    
    /// <summary>
    /// شناسه رویداد مرزی خطا (در صورت وجود)
    /// </summary>
    public string? ErrorBoundaryEventId { get; init; }
    
    /// <inheritdoc/>
    public new string Intent { get; init; } = "FAILED";
}

/// <summary>
/// رویداد درحال خاتمه المان BPMN
/// </summary>
public record ElementTerminating : ElementEvent
{
    /// <summary>
    /// دلیل خاتمه
    /// </summary>
    public string? Reason { get; init; }
    
    /// <inheritdoc/>
    public new string Intent { get; init; } = "TERMINATING";
}

/// <summary>
/// رویداد خاتمه المان BPMN
/// </summary>
public record ElementTerminated : ElementEvent
{
    /// <inheritdoc/>
    public new string Intent { get; init; } = "TERMINATED";
}

/// <summary>
/// رویداد وقوع وضعیت زمانی - برای رویدادهای Timer Event
/// </summary>
public record TimerTriggeredEvent : BpmnEvent
{
    /// <summary>
    /// شناسه رویداد زمانی
    /// </summary>
    public required string TimerEventId { get; init; }
    
    /// <summary>
    /// نوع رویداد زمانی (Start/Intermediate/Boundary)
    /// </summary>
    public required string TimerEventType { get; init; }
    
    /// <summary>
    /// زمان شروع تایمر
    /// </summary>
    public DateTime StartTime { get; init; }
    
    /// <summary>
    /// عبارت تعریف‌کننده زمان
    /// </summary>
    public string? TimerExpression { get; init; }
}

/// <summary>
/// رویداد وقوع پیام - برای رویدادهای Message Event
/// </summary>
public record MessageReceivedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه رویداد پیام
    /// </summary>
    public required string MessageEventId { get; init; }
    
    /// <summary>
    /// نام پیام
    /// </summary>
    public required string MessageName { get; init; }
    
    /// <summary>
    /// کلید همبستگی (correlation key)
    /// </summary>
    public string? CorrelationKey { get; init; }
    
    /// <summary>
    /// محتوای پیام
    /// </summary>
    public Dictionary<string, object>? MessageContent { get; init; }
} 