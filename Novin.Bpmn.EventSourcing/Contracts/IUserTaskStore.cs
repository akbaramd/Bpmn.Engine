using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.EventSourcing.Core.Models;

namespace Novin.Bpmn.EventSourcing.Contracts;

/// <summary>
/// مخزن ذخیره‌سازی و بازیابی وظایف کاربری
/// </summary>
public interface IUserTaskStore
{
    /// <summary>
    /// ذخیره یک وظیفه کاربری جدید
    /// </summary>
    /// <param name="userTask">وظیفه کاربری</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وظیفه ذخیره شده</returns>
    Task<UserTaskInfo> SaveTaskAsync(UserTaskInfo userTask, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// بروزرسانی یک وظیفه کاربری موجود
    /// </summary>
    /// <param name="userTask">وظیفه کاربری</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وظیفه بروزرسانی شده</returns>
    Task<UserTaskInfo> UpdateTaskAsync(UserTaskInfo userTask, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// دریافت یک وظیفه کاربری با شناسه
    /// </summary>
    /// <param name="taskId">شناسه وظیفه</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وظیفه یا null اگر یافت نشد</returns>
    Task<UserTaskInfo?> GetTaskByIdAsync(string taskId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// دریافت وظایف کاربری یک نمونه فرآیند
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
    /// دریافت وظایف کاربری فعال یک المان در یک نمونه فرآیند
    /// </summary>
    /// <param name="processInstanceId">شناسه نمونه فرآیند</param>
    /// <param name="elementId">شناسه المان</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>وظیفه فعال یا null اگر یافت نشد</returns>
    Task<UserTaskInfo?> GetActiveTaskByElementIdAsync(
        string processInstanceId, 
        string elementId, 
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
    /// دریافت تمام وظایف کاربری یک کاربر (تخصیص یافته و کاندیدا)
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
    /// جستجوی وظایف کاربری با معیارهای پیشرفته
    /// </summary>
    /// <param name="searchOptions">گزینه‌های جستجو</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>نتایج جستجو</returns>
    Task<UserTaskSearchResult> SearchTasksAsync(
        UserTaskSearchOptions searchOptions,
        CancellationToken cancellationToken = default);
        
    /// <summary>
    /// حذف وظایف کاربری تکمیل شده یک فرآیند
    /// </summary>
    /// <param name="processInstanceId">شناسه نمونه فرآیند</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>تعداد رکوردهای حذف شده</returns>
    Task<int> DeleteCompletedTasksAsync(string processInstanceId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// حذف تمام وظایف کاربری یک فرآیند
    /// </summary>
    /// <param name="processInstanceId">شناسه نمونه فرآیند</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>تعداد رکوردهای حذف شده</returns>
    Task<int> DeleteAllTasksAsync(string processInstanceId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// بروزرسانی وضعیت وظیفه کاربری
    /// </summary>
    /// <param name="taskId">شناسه وظیفه</param>
    /// <param name="status">وضعیت جدید</param>
    /// <param name="cancellationToken">توکن لغو</param>
    /// <returns>آیا بروزرسانی موفق بود</returns>
    Task<bool> UpdateTaskStatusAsync(
        string taskId, 
        UserTaskStatus status, 
        CancellationToken cancellationToken = default);
} 