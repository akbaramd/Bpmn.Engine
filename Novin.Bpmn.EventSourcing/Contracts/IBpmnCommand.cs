using System;

namespace Novin.Bpmn.EventSourcing.Contracts;

/// <summary>
/// پایه برای تمام دستورات موتور فرآیند BPMN
/// </summary>
public interface IBpmnCommand
{
    /// <summary>
    /// شناسه منحصر به فرد دستور
    /// </summary>
    Guid CommandId { get; }
    
    /// <summary>
    /// شناسه نمونه فرآیند مرتبط با این دستور
    /// </summary>
    string ProcessInstanceId { get; }
    
    /// <summary>
    /// نوع دستور
    /// </summary>
    string CommandType { get; }
    
    /// <summary>
    /// زمان ثبت دستور
    /// </summary>
    DateTime Timestamp { get; }
    
    /// <summary>
    /// نسخه دستور (برای سازگاری نسخه‌های مختلف)
    /// </summary>
    int Version { get; }
    
    /// <summary>
    /// شناسه کاربر درخواست‌کنندهٔ دستور
    /// </summary>
    string? UserId { get; }
    
    /// <summary>
    /// اطلاعات تکمیلی دستور (اختیاری)
    /// </summary>
    Dictionary<string, object>? Metadata { get; }
} 