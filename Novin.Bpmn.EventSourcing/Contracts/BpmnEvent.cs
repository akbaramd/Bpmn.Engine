using System;

namespace Novin.Bpmn.EventSourcing.Contracts;

/// <summary>
/// کلاس پایه برای تمام رویدادهای موتور فرآیند BPMN
/// </summary>
public abstract record BpmnEvent : IBpmnEvent
{
    /// <summary>
    /// شناسه منحصر به فرد رویداد
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// شناسه نمونه فرآیند مرتبط با این رویداد
    /// </summary>
    public required string ProcessInstanceId { get; init; }
    
    /// <summary>
    /// نوع رویداد
    /// </summary>
    public string EventType => GetType().Name;
    
    /// <summary>
    /// زمان ثبت رویداد
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// نسخه رویداد (برای سازگاری نسخه‌های مختلف)
    /// </summary>
    public int Version { get; init; } = 1;
    
    /// <summary>
    /// شناسه کاربر ایجاد کنندهٔ رویداد (اختیاری)
    /// </summary>
    public string? UserId { get; init; }
    
    /// <summary>
    /// اطلاعات تکمیلی رویداد (اختیاری)
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
    
    /// <summary>
    /// شناسه دستور مرتبط با این رویداد (اختیاری)
    /// </summary>
    public Guid? CausationId { get; init; }
    
    /// <summary>
    /// شناسه رویداد قبلی در زنجیره (اختیاری)
    /// </summary>
    public Guid? CorrelationId { get; init; }

    public long Position => throw new NotImplementedException();

    public long Key => throw new NotImplementedException();

    public string Intent => throw new NotImplementedException();

    public int ProcessVersion => throw new NotImplementedException();
} 