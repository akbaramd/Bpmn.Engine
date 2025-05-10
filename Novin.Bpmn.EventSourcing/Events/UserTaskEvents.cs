using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Collections.Generic;

namespace Novin.Bpmn.EventSourcing.Events;

/// <summary>
/// رویداد ایجاد وظیفه کاربری
/// </summary>
public record UserTaskCreatedEvent : BpmnEvent
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
    
    /// <summary>
    /// متغیرهای موردنیاز برای فرم
    /// </summary>
    public Dictionary<string, object>? FormVariables { get; init; }
    
    /// <summary>
    /// اولویت وظیفه
    /// </summary>
    public int Priority { get; init; }
}

/// <summary>
/// رویداد تخصیص وظیفه کاربری
/// </summary>
public record UserTaskAssignedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه وظیفه کاربر
    /// </summary>
    public required string UserTaskId { get; init; }
    
    /// <summary>
    /// شناسه کاربر
    /// </summary>
    public required string UserId { get; init; }
    
    /// <summary>
    /// نام کاربر
    /// </summary>
    public string? UserName { get; init; }
}

/// <summary>
/// رویداد لغو تخصیص وظیفه کاربری
/// </summary>
public record UserTaskUnassignedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه وظیفه کاربر
    /// </summary>
    public required string UserTaskId { get; init; }
    
    /// <summary>
    /// شناسه کاربر
    /// </summary>
    public required string UserId { get; init; }
}

/// <summary>
/// رویداد تکمیل وظیفه کاربری
/// </summary>
public record UserTaskCompletedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه وظیفه کاربر
    /// </summary>
    public required string UserTaskId { get; init; }
    
    /// <summary>
    /// شناسه کاربر
    /// </summary>
    public required string UserId { get; init; }
    
    /// <summary>
    /// داده‌های ارسالی فرم
    /// </summary>
    public Dictionary<string, object>? FormData { get; init; }
}

/// <summary>
/// رویداد افزودن کامنت به وظیفه
/// </summary>
public record TaskCommentAddedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه وظیفه کاربر
    /// </summary>
    public required string UserTaskId { get; init; }
    
    /// <summary>
    /// شناسه کامنت
    /// </summary>
    public required string CommentId { get; init; }
    
    /// <summary>
    /// شناسه کاربر
    /// </summary>
    public required string UserId { get; init; }
    
    /// <summary>
    /// نام کاربر
    /// </summary>
    public string? UserName { get; init; }
    
    /// <summary>
    /// متن کامنت
    /// </summary>
    public required string CommentText { get; init; }
}

/// <summary>
/// رویداد منقضی شدن وظیفه کاربری (سررسید)
/// </summary>
public record UserTaskDueEvent : BpmnEvent
{
    /// <summary>
    /// شناسه وظیفه کاربر
    /// </summary>
    public required string UserTaskId { get; init; }
    
    /// <summary>
    /// تاریخ سررسید
    /// </summary>
    public DateTime DueDate { get; init; }
}

/// <summary>
/// رویداد بروزرسانی اولویت وظیفه
/// </summary>
public record UserTaskPriorityChangedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه وظیفه کاربر
    /// </summary>
    public required string UserTaskId { get; init; }
    
    /// <summary>
    /// اولویت قبلی
    /// </summary>
    public int OldPriority { get; init; }
    
    /// <summary>
    /// اولویت جدید
    /// </summary>
    public int NewPriority { get; init; }
}

/// <summary>
/// رویداد ارسال فرم وظیفه کاربری
/// </summary>
public record UserTaskSubmittedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه وظیفه کاربر
    /// </summary>
    public required string UserTaskId { get; init; }
    
    /// <summary>
    /// داده‌های ارسالی فرم
    /// </summary>
    public Dictionary<string, object>? FormData { get; init; }
    
    /// <summary>
    /// متغیرهای استخراج شده از فرم
    /// </summary>
    public Dictionary<string, object>? OutputVariables { get; init; }
}

/// <summary>
/// رویداد ادعای وظیفه کاربری
/// </summary>
public record UserTaskClaimedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه وظیفه کاربر
    /// </summary>
    public required string UserTaskId { get; init; }
    
    /// <summary>
    /// شناسه کاربری که وظیفه را به خود اختصاص داده
    /// </summary>
    public required string AssigneeId { get; init; }
    
    /// <summary>
    /// نام کاربری که وظیفه را به خود اختصاص داده (برای نمایش)
    /// </summary>
    public string? AssigneeName { get; init; }
} 