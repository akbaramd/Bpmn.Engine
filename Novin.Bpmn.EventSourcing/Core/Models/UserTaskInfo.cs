using System;
using System.Collections.Generic;

namespace Novin.Bpmn.EventSourcing.Core.Models;

/// <summary>
/// اطلاعات وظیفه کاربری
/// </summary>
public class UserTaskInfo
{
    /// <summary>
    /// شناسه وظیفه
    /// </summary>
    public string TaskId { get; set; } = null!;
    
    /// <summary>
    /// شناسه نمونه فرآیند
    /// </summary>
    public string ProcessInstanceId { get; set; } = null!;
    
    /// <summary>
    /// شناسه تعریف فرآیند
    /// </summary>
    public string? ProcessDefinitionId { get; set; }
    
    /// <summary>
    /// کلید تعریف فرآیند
    /// </summary>
    public string? ProcessDefinitionKey { get; set; }
    
    /// <summary>
    /// شناسه المان در مدل BPMN
    /// </summary>
    public string ElementId { get; set; } = null!;
    
    /// <summary>
    /// عنوان وظیفه
    /// </summary>
    public string? TaskTitle { get; set; }
    
    /// <summary>
    /// توضیحات وظیفه
    /// </summary>
    public string? TaskDescription { get; set; }
    
    /// <summary>
    /// مسئول انجام وظیفه
    /// </summary>
    public string? Assignee { get; set; }
    
    /// <summary>
    /// نام مسئول انجام وظیفه
    /// </summary>
    public string? AssigneeName { get; set; }
    
    /// <summary>
    /// زمان تخصیص
    /// </summary>
    public DateTime? AssignedAt { get; set; }
    
    /// <summary>
    /// کاربران کاندیدا
    /// </summary>
    public List<string>? CandidateUsers { get; set; }
    
    /// <summary>
    /// گروه‌های کاندیدا
    /// </summary>
    public List<string>? CandidateGroups { get; set; }
    
    /// <summary>
    /// کلید فرم
    /// </summary>
    public string? FormKey { get; set; }
    
    /// <summary>
    /// متغیرهای فرم
    /// </summary>
    public Dictionary<string, object>? FormVariables { get; set; }
    
    /// <summary>
    /// داده‌های ارسالی فرم
    /// </summary>
    public Dictionary<string, object>? FormData { get; set; }
    
    /// <summary>
    /// زمان سررسید
    /// </summary>
    public DateTime? DueDate { get; set; }
    
    /// <summary>
    /// زمان یادآوری
    /// </summary>
    public DateTime? FollowUpDate { get; set; }
    
    /// <summary>
    /// اولویت (0-100)
    /// </summary>
    public int Priority { get; set; }
    
    /// <summary>
    /// وضعیت
    /// </summary>
    public UserTaskStatus Status { get; set; } = UserTaskStatus.Created;
    
    /// <summary>
    /// تعداد تلاش‌های باقی‌مانده در صورت شکست
    /// </summary>
    public int RemainingRetries { get; set; }
    
    /// <summary>
    /// کامنت‌های وظیفه
    /// </summary>
    public List<UserTaskComment>? Comments { get; set; }
    
    /// <summary>
    /// تگ‌های وظیفه
    /// </summary>
    public List<string>? Tags { get; set; }
    
    /// <summary>
    /// برچسب‌های دلخواه
    /// </summary>
    public Dictionary<string, string>? Labels { get; set; }
    
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
    /// کاربر تکمیل‌کننده
    /// </summary>
    public string? CompletedBy { get; set; }
    
    /// <summary>
    /// نام کاربر تکمیل‌کننده
    /// </summary>
    public string? CompletedByName { get; set; }
    
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

/// <summary>
/// وضعیت وظیفه کاربری
/// </summary>
public enum UserTaskStatus
{
    /// <summary>
    /// ایجاد شده
    /// </summary>
    Created,
    
    /// <summary>
    /// فعال - آماده انجام
    /// </summary>
    Active,
    
    /// <summary>
    /// به کاربر تخصیص داده شده
    /// </summary>
    Assigned,
    
    /// <summary>
    /// در حال انجام
    /// </summary>
    InProgress,
    
    /// <summary>
    /// تکمیل شده
    /// </summary>
    Completed,
    
    /// <summary>
    /// لغو شده
    /// </summary>
    Cancelled,
    
    /// <summary>
    /// با خطا مواجه شده
    /// </summary>
    Error,
    
    /// <summary>
    /// در انتظار رویداد خارجی
    /// </summary>
    Suspended
}

/// <summary>
/// کامنت وظیفه کاربری
/// </summary>
public class UserTaskComment
{
    /// <summary>
    /// شناسه کامنت
    /// </summary>
    public string CommentId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// شناسه کاربر نویسنده
    /// </summary>
    public string UserId { get; set; } = null!;
    
    /// <summary>
    /// نام کاربر نویسنده
    /// </summary>
    public string? UserName { get; set; }
    
    /// <summary>
    /// متن کامنت
    /// </summary>
    public string Text { get; set; } = null!;
    
    /// <summary>
    /// زمان ایجاد
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// آیا حذف شده است
    /// </summary>
    public bool IsDeleted { get; set; }
    
    /// <summary>
    /// زمان حذف
    /// </summary>
    public DateTime? DeletedAt { get; set; }
} 