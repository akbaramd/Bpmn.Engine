using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Collections.Generic;

namespace Novin.Bpmn.EventSourcing.Events;

/// <summary>
/// رویداد فرآیند BPMN
/// </summary>
public abstract record ProcessEvent : BpmnEvent
{
}

/// <summary>
/// رویداد ایجاد نمونه فرآیند BPMN
/// </summary>
public record ProcessInstanceCreating : ProcessEvent
{
    /// <summary>
    /// شناسه تعریف فرآیند
    /// </summary>
    public required string ProcessDefinitionId { get; init; }
    
    /// <summary>
    /// کلید انتشار
    /// </summary>
    public required string DeploymentKey { get; init; }
    
    /// <summary>
    /// XML تعریف BPMN
    /// </summary>
    public required string DefinitionXml { get; init; }
    
    /// <summary>
    /// متغیرهای اولیه فرآیند
    /// </summary>
    public Dictionary<string, object>? InitialVariables { get; init; }
    
    /// <inheritdoc/>
    public new string Intent { get; init; } = "CREATING";
}

/// <summary>
/// رویداد ایجاد شدن نمونه فرآیند BPMN
/// </summary>
public record ProcessInstanceCreated : ProcessEvent
{
    /// <summary>
    /// شناسه تعریف فرآیند
    /// </summary>
    public required string ProcessDefinitionId { get; init; }
    
    /// <summary>
    /// کلید فرآیند
    /// </summary>
    public string? ProcessDefinitionKey { get; init; }
    
    /// <summary>
    /// نسخه تعریف فرآیند
    /// </summary>
    public int ProcessDefinitionVersion { get; init; }
    
    /// <summary>
    /// کلید کسب‌وکار
    /// </summary>
    public string? BusinessKey { get; init; }
    
    /// <summary>
    /// شناسه کاربر شروع‌کننده
    /// </summary>
    public string? StartUserId { get; init; }
    
    /// <summary>
    /// متغیرهای اولیه
    /// </summary>
    public Dictionary<string, object>? Variables { get; init; }
    
    /// <inheritdoc/>
    public new string Intent { get; init; } = "CREATED";
}

/// <summary>
/// رویداد شروع نمونه فرآیند BPMN
/// </summary>
public record ProcessInstanceStarting : ProcessEvent
{
    /// <summary>
    /// شناسه رویداد شروع
    /// </summary>
    public required string StartEventId { get; init; }
    
    /// <summary>
    /// نوع رویداد شروع
    /// </summary>
    public string? StartEventType { get; init; }
    
    /// <inheritdoc/>
    public new string Intent { get; init; } = "STARTING";
}

/// <summary>
/// رویداد شروع شدن نمونه فرآیند BPMN
/// </summary>
public record ProcessInstanceStarted : ProcessEvent
{
    /// <inheritdoc/>
    public new string Intent { get; init; } = "STARTED";
}

/// <summary>
/// رویداد تکمیل نمونه فرآیند BPMN
/// </summary>
public record ProcessInstanceCompleting : ProcessEvent
{
    /// <summary>
    /// شناسه رویداد پایان
    /// </summary>
    public required string EndEventId { get; init; }
    
    /// <summary>
    /// نوع رویداد پایان
    /// </summary>
    public string? EndEventType { get; init; }
    
    /// <summary>
    /// متغیرهای نهایی
    /// </summary>
    public Dictionary<string, object>? FinalVariables { get; init; }
    
    /// <inheritdoc/>
    public new string Intent { get; init; } = "COMPLETING";
}

/// <summary>
/// رویداد تکمیل شدن نمونه فرآیند BPMN
/// </summary>
public record ProcessCompletedEvent : ProcessEvent
{
    /// <summary>
    /// شناسه رویداد پایان که منجر به اتمام فرآیند شده
    /// </summary>
    public string? EndEventId { get; init; }
    
    /// <summary>
    /// متغیرهای نهایی
    /// </summary>
    public Dictionary<string, object>? FinalVariables { get; init; }
    
    /// <inheritdoc/>
    public new string Intent { get; init; } = "COMPLETED";
}

/// <summary>
/// رویداد حذف نمونه فرآیند BPMN
/// </summary>
public record ProcessInstanceDeleting : ProcessEvent
{
    /// <summary>
    /// دلیل حذف
    /// </summary>
    public string? Reason { get; init; }
    
    /// <inheritdoc/>
    public new string Intent { get; init; } = "DELETING";
}

/// <summary>
/// رویداد حذف شدن نمونه فرآیند BPMN
/// </summary>
public record ProcessInstanceDeleted : ProcessEvent
{
    /// <inheritdoc/>
    public new string Intent { get; init; } = "DELETED";
}

/// <summary>
/// رویداد توقف موقت نمونه فرآیند BPMN
/// </summary>
public record ProcessInstanceSuspending : ProcessEvent
{
    /// <summary>
    /// دلیل توقف موقت
    /// </summary>
    public string? Reason { get; init; }
    
    /// <inheritdoc/>
    public new string Intent { get; init; } = "SUSPENDING";
}

/// <summary>
/// رویداد توقف موقت شدن نمونه فرآیند BPMN
/// </summary>
public record ProcessInstanceSuspended : ProcessEvent
{
    /// <inheritdoc/>
    public new string Intent { get; init; } = "SUSPENDED";
}

/// <summary>
/// رویداد ازسرگیری نمونه فرآیند BPMN
/// </summary>
public record ProcessInstanceResuming : ProcessEvent
{
    /// <summary>
    /// دلیل ازسرگیری
    /// </summary>
    public string? Reason { get; init; }
    
    /// <inheritdoc/>
    public new string Intent { get; init; } = "RESUMING";
}

/// <summary>
/// رویداد ازسرگرفته شدن نمونه فرآیند BPMN
/// </summary>
public record ProcessInstanceResumed : ProcessEvent
{
    /// <inheritdoc/>
    public new string Intent { get; init; } = "RESUMED";
}

/// <summary>
/// رویداد بروزرسانی متغیر در فرآیند BPMN
/// </summary>
public record VariableUpdating : ProcessEvent
{
    /// <summary>
    /// نام متغیر
    /// </summary>
    public required string VariableName { get; init; }
    
    /// <summary>
    /// مقدار جدید
    /// </summary>
    public required object Value { get; init; }
    
    /// <summary>
    /// شناسه المان منبع
    /// </summary>
    public string? SourceElementId { get; init; }
    
    /// <inheritdoc/>
    public new string Intent { get; init; } = "UPDATING";
}

/// <summary>
/// رویداد بروزرسانی شدن متغیر در فرآیند BPMN
/// </summary>
public record VariableUpdated : ProcessEvent
{
    /// <summary>
    /// نام متغیر
    /// </summary>
    public required string VariableName { get; init; }
    
    /// <inheritdoc/>
    public new string Intent { get; init; } = "UPDATED";
}

/// <summary>
/// رویداد آماده‌سازی وظیفه کاربر در فرآیند
/// </summary>
public record UserTaskReadyEvent : BpmnEvent
{
    /// <summary>
    /// شناسه وظیفه کاربر
    /// </summary>
    public required string UserTaskId { get; init; }
    
    /// <summary>
    /// عنوان وظیفه
    /// </summary>
    public string? TaskTitle { get; init; }
    
    /// <summary>
    /// توضیحات وظیفه
    /// </summary>
    public string? TaskDescription { get; init; }
    
    /// <summary>
    /// مسئول انجام وظیفه
    /// </summary>
    public string? Assignee { get; init; }
    
    /// <summary>
    /// گروه‌های مسئول
    /// </summary>
    public ICollection<string>? CandidateGroups { get; init; }
    
    /// <summary>
    /// کاربران کاندیدا
    /// </summary>
    public ICollection<string>? CandidateUsers { get; init; }
    
    /// <summary>
    /// فرم مرتبط
    /// </summary>
    public string? FormKey { get; init; }
    
    /// <summary>
    /// زمان سررسید
    /// </summary>
    public DateTime? DueDate { get; init; }
}

/// رویداد آماده‌سازی وظیفه سرویس در فرآیند
/// </summary>
public record ServiceTaskReadyEvent : BpmnEvent
{
    /// <summary>
    /// شناسه وظیفه سرویس
    /// </summary>
    public required string ServiceTaskId { get; init; }
    
    /// <summary>
    /// نوع سرویس
    /// </summary>
    public string? ServiceType { get; init; }
    
    /// <summary>
    /// پارامترهای سرویس
    /// </summary>
    public Dictionary<string, object>? Parameters { get; init; }
}

/// <summary>
/// رویداد تکمیل وظیفه سرویس در فرآیند
/// </summary>
public record ServiceTaskCompletedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه وظیفه سرویس
    /// </summary>
    public required string ServiceTaskId { get; init; }
    
    /// <summary>
    /// نتیجه اجرای سرویس
    /// </summary>
    public object? Result { get; init; }
}

/// <summary>
/// رویداد آماده‌سازی دروازه در فرآیند
/// </summary>
public record GatewayReadyEvent : BpmnEvent
{
    /// <summary>
    /// شناسه دروازه
    /// </summary>
    public required string GatewayId { get; init; }
    
    /// <summary>
    /// نوع دروازه (Exclusive, Inclusive, Parallel, etc.)
    /// </summary>
    public required string GatewayType { get; init; }
    
    /// <summary>
    /// جریان‌های ورودی به دروازه
    /// </summary>
    public ICollection<string>? IncomingFlowIds { get; init; }
    
    /// <summary>
    /// جریان‌های خروجی از دروازه
    /// </summary>
    public ICollection<string>? OutgoingFlowIds { get; init; }
} 