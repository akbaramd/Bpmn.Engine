using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Collections.Generic;

namespace Novin.Bpmn.EventSourcing.Events;

/// <summary>
/// رویداد ایجاد اشتراک تایمر
/// </summary>
public record TimerSubscriptionCreatedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه المان
    /// </summary>
    public required string ElementId { get; init; }
    
    /// <summary>
    /// شناسه تایمر
    /// </summary>
    public required string TimerId { get; init; }
    
    /// <summary>
    /// نوع تایمر (date, duration, cycle)
    /// </summary>
    public required string TimerType { get; init; }
    
    /// <summary>
    /// مقدار تایمر
    /// </summary>
    public string? TimerValue { get; init; }
    
    /// <summary>
    /// شناسه المانی که تایمر به آن متصل شده (برای رویدادهای مرزی)
    /// </summary>
    public string? AttachedToElementId { get; init; }
    
    /// <summary>
    /// آیا این تایمر وقفه‌دهنده است
    /// </summary>
    public bool IsInterrupting { get; init; }
    
    /// <summary>
    /// شناسه مسیر اجرا
    /// </summary>
    public string? ExecutionId { get; init; }
}

/// <summary>
/// رویداد ایجاد اشتراک خطا
/// </summary>
public record ErrorCatchSubscriptionCreatedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه المان
    /// </summary>
    public required string ElementId { get; init; }
    
    /// <summary>
    /// کد خطا
    /// </summary>
    public string? ErrorCode { get; init; }
    
    /// <summary>
    /// نام خطا
    /// </summary>
    public string? ErrorName { get; init; }
    
    /// <summary>
    /// شناسه المانی که رویداد خطا به آن متصل شده (برای رویدادهای مرزی)
    /// </summary>
    public string? AttachedToElementId { get; init; }
    
    /// <summary>
    /// آیا این رویداد خطا وقفه‌دهنده است (معمولاً برای خطا همیشه true است)
    /// </summary>
    public bool IsInterrupting { get; init; } = true;
}

/// <summary>
/// رویداد ایجاد اشتراک پیام
/// </summary>
public record MessageSubscriptionCreatedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه المان
    /// </summary>
    public required string ElementId { get; init; }
    
    /// <summary>
    /// نام پیام
    /// </summary>
    public required string MessageName { get; init; }
    
    /// <summary>
    /// کلید همبستگی
    /// </summary>
    public string? CorrelationKey { get; init; }
    
    /// <summary>
    /// شناسه المانی که رویداد پیام به آن متصل شده (برای رویدادهای مرزی)
    /// </summary>
    public string? AttachedToElementId { get; init; }
    
    /// <summary>
    /// آیا این رویداد پیام وقفه‌دهنده است
    /// </summary>
    public bool IsInterrupting { get; init; }
    
    /// <summary>
    /// شناسه مسیر اجرا
    /// </summary>
    public string? ExecutionId { get; init; }
}

/// <summary>
/// رویداد ایجاد اشتراک سیگنال
/// </summary>
public record SignalSubscriptionCreatedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه المان
    /// </summary>
    public required string ElementId { get; init; }
    
    /// <summary>
    /// نام سیگنال
    /// </summary>
    public required string SignalName { get; init; }
    
    /// <summary>
    /// شناسه المانی که رویداد سیگنال به آن متصل شده (برای رویدادهای مرزی)
    /// </summary>
    public string? AttachedToElementId { get; init; }
    
    /// <summary>
    /// آیا این رویداد سیگنال وقفه‌دهنده است
    /// </summary>
    public bool IsInterrupting { get; init; }
}

/// <summary>
/// رویداد ایجاد اشتراک شرطی
/// </summary>
public record ConditionalSubscriptionCreatedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه المان
    /// </summary>
    public required string ElementId { get; init; }
    
    /// <summary>
    /// شرط
    /// </summary>
    public required string Condition { get; init; }
    
    /// <summary>
    /// شناسه المانی که رویداد شرطی به آن متصل شده (برای رویدادهای مرزی)
    /// </summary>
    public string? AttachedToElementId { get; init; }
    
    /// <summary>
    /// آیا این رویداد شرطی وقفه‌دهنده است
    /// </summary>
    public bool IsInterrupting { get; init; }
}

/// <summary>
/// رویداد ایجاد اشتراک escalation
/// </summary>
public record EscalationSubscriptionCreatedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه المان
    /// </summary>
    public required string ElementId { get; init; }
    
    /// <summary>
    /// کد escalation
    /// </summary>
    public required string EscalationCode { get; init; }
    
    /// <summary>
    /// شناسه المانی که رویداد escalation به آن متصل شده (برای رویدادهای مرزی)
    /// </summary>
    public string? AttachedToElementId { get; init; }
    
    /// <summary>
    /// آیا این رویداد escalation وقفه‌دهنده است
    /// </summary>
    public bool IsInterrupting { get; init; }
}

/// <summary>
/// رویداد دریافت پیام
/// </summary>
public record MessageReceivedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه المان
    /// </summary>
    public string ElementId { get; init; }
    
    /// <summary>
    /// نام پیام
    /// </summary>
    public string MessageName { get; init; }
    
    /// <summary>
    /// کلید همبستگی
    /// </summary>
    public string? CorrelationKey { get; init; }
    
    /// <summary>
    /// محتوای پیام
    /// </summary>
    public object? MessageContent { get; init; }
    
    /// <summary>
    /// شناسه مسیر اجرا
    /// </summary>
    public string? ExecutionId { get; init; }
}

