using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Collections.Generic;

namespace Novin.Bpmn.EventSourcing.Events;

/// <summary>
/// رویداد ایجاد کار جدید
/// </summary>
public record JobCreatedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه کار
    /// </summary>
    public required string JobId { get; init; }
    
    /// <summary>
    /// شناسه المان
    /// </summary>
    public required string ElementId { get; init; }
    
    /// <summary>
    /// نوع المان
    /// </summary>
    public required string ElementType { get; init; }
    
    /// <summary>
    /// نوع کار
    /// </summary>
    public required string JobType { get; init; }
    
    /// <summary>
    /// تعداد مجدد تلاش‌ها
    /// </summary>
    public int Retries { get; init; } = 3;
    
    /// <summary>
    /// ضرب‌الاجل
    /// </summary>
    public DateTime? Deadline { get; init; }
    
    /// <summary>
    /// متغیرهای مورد نیاز برای انجام کار
    /// </summary>
    public Dictionary<string, object>? Variables { get; init; }
    
    /// <summary>
    /// هدرهای اختصاصی
    /// </summary>
    public Dictionary<string, string>? CustomHeaders { get; init; }
    
    /// <summary>
    /// تنظیمات اختصاصی کار
    /// </summary>
    public Dictionary<string, object>? JobConfig { get; init; }
}

/// <summary>
/// رویداد فعال‌سازی کار
/// </summary>
public record JobActivatedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه کار
    /// </summary>
    public required string JobId { get; init; }
    
    /// <summary>
    /// شناسه کارگر
    /// </summary>
    public required string WorkerId { get; init; }
    
    /// <summary>
    /// ضرب‌الاجل جدید
    /// </summary>
    public DateTime Deadline { get; init; }
}

/// <summary>
/// رویداد تکمیل کار
/// </summary>
public record JobCompletedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه کار
    /// </summary>
    public required string JobId { get; init; }
    
    /// <summary>
    /// متغیرهای خروجی
    /// </summary>
    public Dictionary<string, object>? Variables { get; init; }
}

/// <summary>
/// رویداد شکست کار
/// </summary>
public record JobFailedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه کار
    /// </summary>
    public required string JobId { get; init; }
    
    /// <summary>
    /// پیام خطا
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// تلاش‌های باقی‌مانده
    /// </summary>
    public int RemainingRetries { get; init; }
    
    /// <summary>
    /// زمان کاری بعدی
    /// </summary>
    public DateTime? RetryBackOff { get; init; }
}

/// <summary>
/// رویداد اتمام زمان کار
/// </summary>
public record JobTimeoutEvent : BpmnEvent
{
    /// <summary>
    /// شناسه کار
    /// </summary>
    public required string JobId { get; init; }
}

/// <summary>
/// رویداد ثبت خطای کار
/// </summary>
public record JobErrorEvent : BpmnEvent
{
    /// <summary>
    /// شناسه کار
    /// </summary>
    public required string JobId { get; init; }
    
    /// <summary>
    /// کد خطا
    /// </summary>
    public required string ErrorCode { get; init; }
    
    /// <summary>
    /// پیام خطا
    /// </summary>
    public string? ErrorMessage { get; init; }
} 