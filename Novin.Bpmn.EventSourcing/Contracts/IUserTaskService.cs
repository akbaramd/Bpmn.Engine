using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.EventSourcing.Core.Models;

namespace Novin.Bpmn.EventSourcing.Contracts;

/// <summary>
/// سرویس مدیریت وظایف کاربری BPMN
/// </summary>
public interface IUserTaskService
{
    /// <summary>
    /// دریافت یک وظیفه کاربری با شناسه
    /// </summary>
    /// <param name="taskId">شناسه وظیفه</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>اطلاعات وظیفه کاربری یا null اگر یافت نشد</returns>
    Task<UserTaskInfo?> GetTaskByIdAsync(string taskId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// دریافت وظایف کاربری یک فرآیند
    /// </summary>
    /// <param name="processInstanceId">شناسه نمونه فرآیند</param>
    /// <param name="includeCompleted">آیا وظایف تکمیل شده هم شامل شوند</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>لیست وظایف کاربری</returns>
    Task<List<UserTaskInfo>> GetTasksByProcessInstanceAsync(
        string processInstanceId, 
        bool includeCompleted = false, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// دریافت وظایف کاربری تخصیص داده شده به یک کاربر
    /// </summary>
    /// <param name="userId">شناسه کاربر</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>لیست وظایف کاربری</returns>
    Task<List<UserTaskInfo>> GetTasksAssignedToUserAsync(
        string userId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// دریافت وظایف کاربری قابل دسترسی برای یک کاربر (تخصیص یافته یا کاندیدا)
    /// </summary>
    /// <param name="userId">شناسه کاربر</param>
    /// <param name="userGroups">گروه‌های کاربر (اختیاری)</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>لیست وظایف کاربری</returns>
    Task<List<UserTaskInfo>> GetAvailableTasksForUserAsync(
        string userId,
        ICollection<string>? userGroups = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// جستجوی وظایف کاربری با معیارهای مختلف
    /// </summary>
    /// <param name="searchOptions">گزینه‌های جستجو</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>نتایج جستجو</returns>
    Task<UserTaskSearchResult> SearchTasksAsync(
        UserTaskSearchOptions searchOptions,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// تخصیص وظیفه کاربری به یک کاربر
    /// </summary>
    /// <param name="taskId">شناسه وظیفه</param>
    /// <param name="userId">شناسه کاربر</param>
    /// <param name="userName">نام کاربر (اختیاری)</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وظیفه به‌روزرسانی شده</returns>
    Task<UserTaskInfo> AssignTaskAsync(
        string taskId,
        string userId,
        string? userName = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// لغو تخصیص وظیفه کاربری
    /// </summary>
    /// <param name="taskId">شناسه وظیفه</param>
    /// <param name="userId">شناسه کاربر فعلی (برای بررسی مجوز)</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وظیفه به‌روزرسانی شده</returns>
    Task<UserTaskInfo> UnassignTaskAsync(
        string taskId,
        string userId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// تکمیل وظیفه کاربری
    /// </summary>
    /// <param name="taskId">شناسه وظیفه</param>
    /// <param name="userId">شناسه کاربر تکمیل‌کننده</param>
    /// <param name="formData">داده‌های فرم (اختیاری)</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وظیفه به‌روزرسانی شده</returns>
    Task<UserTaskInfo> CompleteTaskAsync(
        string taskId,
        string userId,
        Dictionary<string, object>? formData = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// ایجاد یک وظیفه کاربری جدید (معمولاً توسط موتور BPMN فراخوانی می‌شود)
    /// </summary>
    /// <param name="taskInfo">اطلاعات وظیفه جدید</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وظیفه ایجاد شده</returns>
    Task<UserTaskInfo> CreateTaskAsync(
        UserTaskInfo taskInfo,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// تنظیم تاریخ سررسید برای وظیفه
    /// </summary>
    /// <param name="taskId">شناسه وظیفه</param>
    /// <param name="dueDate">تاریخ سررسید</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وظیفه به‌روزرسانی شده</returns>
    Task<UserTaskInfo> SetDueDateAsync(
        string taskId,
        DateTime dueDate,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// اضافه کردن کامنت به وظیفه
    /// </summary>
    /// <param name="taskId">شناسه وظیفه</param>
    /// <param name="userId">شناسه کاربر نویسنده</param>
    /// <param name="userName">نام کاربر نویسنده</param>
    /// <param name="commentText">متن کامنت</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>شناسه کامنت اضافه شده</returns>
    Task<string> AddCommentAsync(
        string taskId,
        string userId,
        string userName,
        string commentText,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// تنظیم اولویت وظیفه
    /// </summary>
    /// <param name="taskId">شناسه وظیفه</param>
    /// <param name="priority">اولویت (0-100)</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وظیفه به‌روزرسانی شده</returns>
    Task<UserTaskInfo> SetPriorityAsync(
        string taskId,
        int priority,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// بروزرسانی متغیرهای فرم وظیفه
    /// </summary>
    /// <param name="taskId">شناسه وظیفه</param>
    /// <param name="formVariables">متغیرهای فرم جدید</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وظیفه به‌روزرسانی شده</returns>
    Task<UserTaskInfo> UpdateFormVariablesAsync(
        string taskId,
        Dictionary<string, object> formVariables,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// تنظیم گروه‌های کاندیدا برای وظیفه
    /// </summary>
    /// <param name="taskId">شناسه وظیفه</param>
    /// <param name="candidateGroups">گروه‌های کاندیدا</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وظیفه به‌روزرسانی شده</returns>
    Task<UserTaskInfo> SetCandidateGroupsAsync(
        string taskId,
        ICollection<string> candidateGroups,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// تنظیم کاربران کاندیدا برای وظیفه
    /// </summary>
    /// <param name="taskId">شناسه وظیفه</param>
    /// <param name="candidateUsers">کاربران کاندیدا</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وظیفه به‌روزرسانی شده</returns>
    Task<UserTaskInfo> SetCandidateUsersAsync(
        string taskId,
        ICollection<string> candidateUsers,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// گزینه‌های جستجوی وظایف کاربری
/// </summary>
public class UserTaskSearchOptions
{
    /// <summary>
    /// جستجو بر اساس شناسه نمونه فرآیند
    /// </summary>
    public string? ProcessInstanceId { get; set; }
    
    /// <summary>
    /// جستجو بر اساس شناسه کاربر تخصیص یافته
    /// </summary>
    public string? AssigneeId { get; set; }
    
    /// <summary>
    /// جستجو بر اساس کاربر کاندیدا
    /// </summary>
    public string? CandidateUserId { get; set; }
    
    /// <summary>
    /// جستجو بر اساس گروه کاندیدا
    /// </summary>
    public string? CandidateGroupId { get; set; }
    
    /// <summary>
    /// شامل وظایف تکمیل شده
    /// </summary>
    public bool IncludeCompleted { get; set; }
    
    /// <summary>
    /// شامل وظایف فعال
    /// </summary>
    public bool IncludeActive { get; set; } = true;
    
    /// <summary>
    /// جستجو بر اساس عنوان (قسمتی از عنوان)
    /// </summary>
    public string? TitleContains { get; set; }
    
    /// <summary>
    /// جستجو بر اساس تاریخ ایجاد (از این تاریخ به بعد)
    /// </summary>
    public DateTime? CreatedAfter { get; set; }
    
    /// <summary>
    /// جستجو بر اساس تاریخ ایجاد (تا این تاریخ)
    /// </summary>
    public DateTime? CreatedBefore { get; set; }
    
    /// <summary>
    /// جستجو بر اساس تاریخ سررسید (از این تاریخ به بعد)
    /// </summary>
    public DateTime? DueAfter { get; set; }
    
    /// <summary>
    /// جستجو بر اساس تاریخ سررسید (تا این تاریخ)
    /// </summary>
    public DateTime? DueBefore { get; set; }
    
    /// <summary>
    /// حداقل اولویت
    /// </summary>
    public int? MinPriority { get; set; }
    
    /// <summary>
    /// حداکثر تعداد نتایج
    /// </summary>
    public int MaxResults { get; set; } = 100;
    
    /// <summary>
    /// برگه (برای صفحه‌بندی)
    /// </summary>
    public int Page { get; set; } = 0;
    
    /// <summary>
    /// مرتب‌سازی بر اساس فیلد
    /// </summary>
    public string? SortBy { get; set; }
    
    /// <summary>
    /// ترتیب نزولی
    /// </summary>
    public bool SortDescending { get; set; }
}

/// <summary>
/// نتیجه جستجوی وظایف کاربری
/// </summary>
public class UserTaskSearchResult
{
    /// <summary>
    /// تعداد کل نتایج (بدون در نظر گرفتن صفحه‌بندی)
    /// </summary>
    public int TotalCount { get; set; }
    
    /// <summary>
    /// لیست وظایف یافت شده
    /// </summary>
    public List<UserTaskInfo> Tasks { get; set; } = new();
    
    /// <summary>
    /// شماره صفحه فعلی
    /// </summary>
    public int Page { get; set; }
    
    /// <summary>
    /// اندازه هر صفحه
    /// </summary>
    public int PageSize { get; set; }
    
    /// <summary>
    /// آیا صفحه بعدی وجود دارد
    /// </summary>
    public bool HasNextPage { get; set; }
} 