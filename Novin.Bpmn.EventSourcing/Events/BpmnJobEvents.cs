using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Collections.Generic;

namespace Novin.Bpmn.EventSourcing.Events;

/// <summary>
/// رویداد ایجاد کار
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
    /// تعداد تلاش مجدد
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
    
    /// <summary>
    /// شناسه مسیر اجرا
    /// </summary>
    public string? ExecutionId { get; init; }
}

/// <summary>
/// رویداد شروع اجرای کار
/// </summary>
public record JobStartedEvent : BpmnEvent
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
    /// شناسه مسیر اجرا
    /// </summary>
    public string? ExecutionId { get; init; }
}

/// <summary>
/// رویداد تکمیل اجرای کار
/// </summary>
public record JobCompletedEvent : BpmnEvent
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
    /// نتیجه کار
    /// </summary>
    public object? Result { get; init; }
    
    /// <summary>
    /// شناسه مسیر اجرا
    /// </summary>
    public string? ExecutionId { get; init; }
}

/// <summary>
/// رویداد شکست اجرای کار
/// </summary>
public record JobFailedEvent : BpmnEvent
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
    /// کد خطا
    /// </summary>
    public required string ErrorCode { get; init; }
    
    /// <summary>
    /// پیام خطا
    /// </summary>
    public required string ErrorMessage { get; init; }
    
    /// <summary>
    /// تعداد تلاش باقیمانده
    /// </summary>
    public int RemainingRetries { get; init; }
    
    /// <summary>
    /// زمان کاری بعدی
    /// </summary>
    public DateTime? RetryBackOff { get; init; }
    
    /// <summary>
    /// شناسه مسیر اجرا
    /// </summary>
    public string? ExecutionId { get; init; }
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