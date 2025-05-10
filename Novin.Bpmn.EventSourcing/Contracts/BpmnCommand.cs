using System;

namespace Novin.Bpmn.EventSourcing.Contracts;

/// <summary>
/// کلاس پایه برای تمام دستورات موتور فرآیند BPMN
/// </summary>
public abstract record BpmnCommand : IBpmnCommand
{
    /// <summary>
    /// شناسه منحصر به فرد دستور
    /// </summary>
    public Guid CommandId { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// شناسه نمونه فرآیند مرتبط با این دستور
    /// </summary>
    public required string ProcessInstanceId { get; init; }
    
    /// <summary>
    /// نوع دستور
    /// </summary>
    public string CommandType => GetType().Name;
    
    /// <summary>
    /// زمان ثبت دستور
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// نسخه دستور (برای سازگاری نسخه‌های مختلف)
    /// </summary>
    public int Version { get; init; } = 1;
    
    /// <summary>
    /// شناسه کاربر درخواست‌کنندهٔ دستور
    /// </summary>
    public string? UserId { get; init; }
    
    /// <summary>
    /// اطلاعات تکمیلی دستور (اختیاری)
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
} 