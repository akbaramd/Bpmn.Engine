using System;

namespace Novin.Bpmn.EventSourcing.Core.Models;

/// <summary>
/// وضعیت یک المان BPMN
/// </summary>
public class ElementStatus
{
    /// <summary>
    /// شناسه المان
    /// </summary>
    public string ElementId { get; set; } = null!;
    
    /// <summary>
    /// نوع المان
    /// </summary>
    public string ElementType { get; set; } = null!;
    
    /// <summary>
    /// وضعیت المان
    /// </summary>
    public string Status { get; set; } = null!;
    
    /// <summary>
    /// زمان ایجاد
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// زمان آخرین بروزرسانی
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// زمان تکمیل
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// پیام خطا در صورت شکست
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// کد خطا در صورت شکست
    /// </summary>
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// زمان آخرین خطا
    /// </summary>
    public DateTime? LastErrorAt { get; set; }
} 