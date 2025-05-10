using System;

namespace Novin.Bpmn.EventSourcing.Contracts;

/// <summary>
/// رابط اصلی رویدادهای BPMN
/// </summary>
public interface IBpmnEvent
{
    /// <summary>
    /// شناسه رویداد
    /// </summary>
    Guid EventId { get; }
    
    /// <summary>
    /// شناسه نمونه فرآیند
    /// </summary>
    string ProcessInstanceId { get; }
    
    /// <summary>
    /// نوع رویداد
    /// </summary>
    string EventType { get; }
    
    /// <summary>
    /// زمان رویداد
    /// </summary>
    DateTime Timestamp { get; }
    
    /// <summary>
    /// شناسه کاربر
    /// </summary>
    string? UserId { get; }
    
    /// <summary>
    /// شماره ترتیب (شماره‌ای که نشان‌دهنده ترتیب رویدادها است)
    /// </summary>
    long Position { get; }
    
    /// <summary>
    /// شماره کلید
    /// </summary>
    long Key { get; }
    
    /// <summary>
    /// قصد رویداد (مانند CREATE, ACTIVATE, COMPLETE, ...)
    /// </summary>
    string Intent { get; }
    
    /// <summary>
    /// شناسه نسخه‌ی فرآیند
    /// </summary>
    int ProcessVersion { get; }
} 