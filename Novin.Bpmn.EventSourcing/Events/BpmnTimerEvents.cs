using Novin.Bpmn.EventSourcing.Contracts;
using System;

namespace Novin.Bpmn.EventSourcing.Events;

/// <summary>
/// رویداد فعال شدن تایمر BPMN
/// </summary>
public record TimerTriggeredEvent : BpmnEvent
{
    /// <summary>
    /// شناسه رویداد تایمر
    /// </summary>
    public required string TimerEventId { get; init; }
    
    /// <summary>
    /// نوع رویداد تایمر (start, intermediate, boundary)
    /// </summary>
    public required string TimerEventType { get; init; }
    
    /// <summary>
    /// زمان شروع تایمر
    /// </summary>
    public DateTime StartTime { get; init; }
    
    /// <summary>
    /// عبارت زمانی
    /// </summary>
    public string? TimerExpression { get; init; }
    
    /// <summary>
    /// شناسه اجرا - برای پیگیری مسیر اجرای فرآیند
    /// </summary>
    public string? ExecutionId { get; init; }
    
    /// <inheritdoc/>
    public new string Intent { get; init; } = "TRIGGERED";
} 