using System;
using System.Collections.Generic;

namespace Novin.Bpmn.EventSourcing.Core.Models;

/// <summary>
/// اطلاعات وظیفه در فرآیند BPMN
/// Information about a task in a BPMN process
/// </summary>
public class TaskInfo
{
    /// <summary>
    /// شناسه وظیفه
    /// Task ID
    /// </summary>
    public string TaskId { get; set; } = null!;
    
    /// <summary>
    /// عنوان وظیفه
    /// Task title
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// توضیحات وظیفه
    /// Task description
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// نوع وظیفه
    /// Task type (UserTask, ServiceTask, etc.)
    /// </summary>
    public string TaskType { get; set; } = null!;
    
    /// <summary>
    /// تخصیص دهنده
    /// Assignee ID (user ID)
    /// </summary>
    public string? Assignee { get; set; }
    
    /// <summary>
    /// نام تخصیص دهنده
    /// Assignee name
    /// </summary>
    public string? AssigneeName { get; set; }
    
    /// <summary>
    /// کاربران کاندیدا
    /// Candidate users who can claim this task
    /// </summary>
    public List<string>? CandidateUsers { get; set; }
    
    /// <summary>
    /// گروه‌های کاندیدا
    /// Candidate groups who can claim this task
    /// </summary>
    public List<string>? CandidateGroups { get; set; }
    
    /// <summary>
    /// تاریخ ایجاد
    /// Creation date
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// تاریخ سررسید
    /// Due date
    /// </summary>
    public DateTime? DueDate { get; set; }
    
    /// <summary>
    /// تاریخ یادآوری
    /// Follow-up date
    /// </summary>
    public DateTime? FollowUpDate { get; set; }
    
    /// <summary>
    /// اولویت
    /// Priority
    /// </summary>
    public int Priority { get; set; } = 50;
    
    /// <summary>
    /// فرم مرتبط
    /// Related form ID or key
    /// </summary>
    public string? FormKey { get; set; }
    
    /// <summary>
    /// وضعیت وظیفه
    /// Task status
    /// </summary>
    public TaskStatus Status { get; set; } = TaskStatus.Active;
    
    /// <summary>
    /// تاریخ تکمیل
    /// Completion date
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// اطلاعات فرم
    /// Form data
    /// </summary>
    public Dictionary<string, object>? FormData { get; set; }
    
    /// <summary>
    /// خواص سفارشی
    /// Custom properties
    /// </summary>
    public Dictionary<string, string>? CustomProperties { get; set; }
    
    /// <summary>
    /// شناسه اجرا
    /// Execution ID - for tracking execution path
    /// </summary>
    public string? ExecutionId { get; set; }
}

/// <summary>
/// وضعیت وظیفه
/// Task status
/// </summary>
