using Novin.Bpmn.EventSourcing.Contracts;
using System;

namespace Novin.Bpmn.EventSourcing.Events;

/// <summary>
/// رویداد لغو فعالیت
/// زمانی منتشر می‌شود که یک فعالیت به صورت اجباری لغو شود
/// مانند زمانی که رویداد مرزی از نوع قطع‌کننده فعال می‌شود
/// </summary>
public record ActivityCancelledEvent : BpmnEvent
{
    /// <summary>
    /// شناسه فعالیت لغو شده
    /// </summary>
    public required string ElementId { get; init; }
    
    /// <summary>
    /// دلیل لغو فعالیت
    /// </summary>
    public string? Reason { get; init; }
} 