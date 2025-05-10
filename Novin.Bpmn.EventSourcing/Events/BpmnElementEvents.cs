using Novin.Bpmn.EventSourcing.Contracts;
using System;

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
/// رویداد ایجاد المان BPMN
/// </summary>
public record ElementCreated : ElementEvent
{
    /// <inheritdoc/>
    public new string Intent { get; init; } = "CREATED";
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
    
    /// <inheritdoc/>
    public new string Intent { get; init; } = "FAILED";
}

/// <summary>
/// رویداد خاتمه المان BPMN
/// </summary>
public record ElementTerminated : ElementEvent
{
    /// <inheritdoc/>
    public new string Intent { get; init; } = "TERMINATED";
} 