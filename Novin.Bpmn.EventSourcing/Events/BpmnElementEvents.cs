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

    /// <summary>
    /// شناسه اجرا - برای پیگیری مسیر اجرای فرآیند
    /// </summary>
    public string? ExecutionId { get; init; }
    
    /// <summary>
    /// آیا المان قابل اجرا است
    /// Indicates if the element should execute its business logic
    /// </summary>
    public bool IsExecutable { get; init; } = true;
}

/// <summary>
/// رویداد ایجاد المان BPMN
/// </summary>
public record ElementCreated : ElementEvent
{
    /// <inheritdoc/>
    public new string Intent { get; init; } = "CREATED";
    
    /// <summary>
    /// شناسه المان منبع
    /// </summary>
    public string? SourceElementId { get; init; }
    
    /// <summary>
    /// شناسه جریان توالی
    /// </summary>
    public string? SequenceFlowId { get; init; }
}

/// <summary>
/// رویداد درحال پردازش المان BPMN
/// </summary>
public record ElementProcessing : ElementEvent
{
    /// <summary>
    /// وضعیت پیشرفت پردازش (0 تا 100)
    /// </summary>
    public int Progress { get; init; }
    
    /// <summary>
    /// جزئیات وضعیت پردازش
    /// </summary>
    public string? ProcessingDetails { get; init; }
    
    /// <inheritdoc/>
    public new string Intent { get; init; } = "PROCESSING";
}

/// <summary>
/// رویداد در حال تکمیل المان BPMN
/// </summary>
public record ElementCompleting : ElementEvent
{
    /// <summary>
    /// شناسه‌های جریان‌های خروجی
    /// </summary>
    public List<string>? OutgoingFlowIds { get; init; }
    
    /// <summary>
    /// خروجی المان
    /// </summary>
    public object? Output { get; init; }
    
    /// <summary>
    /// متغیرهای بروزرسانی شده
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
    /// آیا رویداد مرزی خطا دارد
    /// </summary>
    public bool HasErrorBoundaryEvent { get; init; }
    
    /// <summary>
    /// شناسه رویداد مرزی خطا
    /// </summary>
    public string? ErrorBoundaryEventId { get; init; }
    
    /// <inheritdoc/>
    public new string Intent { get; init; } = "FAILED";
}

/// <summary>
/// رویداد در حال خاتمه المان BPMN
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