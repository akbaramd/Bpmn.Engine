using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// پیاده‌سازی مخزن ذخیره‌سازی و بازیابی وظایف کاربری در حافظه
/// </summary>
public class InMemoryUserTaskStore : IUserTaskStore
{
    private readonly ConcurrentDictionary<string, UserTaskInfo> _tasks = new();
    private readonly ILogger<InMemoryUserTaskStore> _logger;
    
    /// <summary>
    /// ایجاد یک نمونه جدید از مخزن وظایف کاربری در حافظه
    /// </summary>
    /// <param name="logger">سیستم ثبت وقایع</param>
    public InMemoryUserTaskStore(ILogger<InMemoryUserTaskStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc />
    public Task<UserTaskInfo> SaveTaskAsync(UserTaskInfo userTask, CancellationToken cancellationToken = default)
    {
        if (userTask == null)
            throw new ArgumentNullException(nameof(userTask));
            
        if (string.IsNullOrEmpty(userTask.TaskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(userTask));

        // ایجاد یک کپی از وظیفه برای جلوگیری از تغییرات خارجی
        var taskCopy = CreateDeepCopy(userTask);
        
        if (!_tasks.TryAdd(taskCopy.TaskId, taskCopy))
        {
            throw new InvalidOperationException($"Task with ID {taskCopy.TaskId} already exists");
        }
        
        _logger.LogDebug("Saved user task {TaskId} for process {ProcessInstanceId}", 
            taskCopy.TaskId, taskCopy.ProcessInstanceId);
            
        return Task.FromResult(taskCopy);
    }
    
    /// <inheritdoc />
    public Task<UserTaskInfo> UpdateTaskAsync(UserTaskInfo userTask, CancellationToken cancellationToken = default)
    {
        if (userTask == null)
            throw new ArgumentNullException(nameof(userTask));
            
        if (string.IsNullOrEmpty(userTask.TaskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(userTask));
            
        // ایجاد یک کپی از وظیفه برای جلوگیری از تغییرات خارجی
        var taskCopy = CreateDeepCopy(userTask);
        
        if (!_tasks.TryGetValue(taskCopy.TaskId, out _))
        {
            throw new KeyNotFoundException($"Task with ID {taskCopy.TaskId} not found");
        }
        
        _tasks[taskCopy.TaskId] = taskCopy;
        
        _logger.LogDebug("Updated user task {TaskId} for process {ProcessInstanceId}", 
            taskCopy.TaskId, taskCopy.ProcessInstanceId);
            
        return Task.FromResult(taskCopy);
    }
    
    /// <inheritdoc />
    public Task<UserTaskInfo?> GetTaskByIdAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));
            
        _tasks.TryGetValue(taskId, out var task);
        
        // اگر وظیفه یافت شد، یک کپی برگردانده می‌شود
        var result = task != null ? CreateDeepCopy(task) : null;
        
        return Task.FromResult(result);
    }
    
    /// <inheritdoc />
    public Task<List<UserTaskInfo>> GetTasksByProcessInstanceAsync(
        string processInstanceId, 
        bool includeCompleted = false, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be null or empty", nameof(processInstanceId));
            
        var tasks = _tasks.Values
            .Where(t => t.ProcessInstanceId == processInstanceId)
            .Where(t => includeCompleted || 
                       (t.Status != UserTaskStatus.Completed && 
                        t.Status != UserTaskStatus.Cancelled))
            .OrderByDescending(t => t.CreatedAt)
            .ToList();
            
        // ایجاد کپی از تمام وظایف
        var result = tasks.Select(CreateDeepCopy).ToList();
        
        _logger.LogDebug("Retrieved {Count} tasks for process {ProcessInstanceId}", 
            result.Count, processInstanceId);
            
        return Task.FromResult(result);
    }
    
    /// <inheritdoc />
    public Task<UserTaskInfo?> GetActiveTaskByElementIdAsync(
        string processInstanceId, 
        string elementId, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be null or empty", nameof(processInstanceId));
            
        if (string.IsNullOrEmpty(elementId))
            throw new ArgumentException("Element ID cannot be null or empty", nameof(elementId));
            
        var task = _tasks.Values
            .Where(t => t.ProcessInstanceId == processInstanceId && t.ElementId == elementId)
            .Where(t => t.Status != UserTaskStatus.Completed && t.Status != UserTaskStatus.Cancelled)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefault();
            
        // اگر وظیفه یافت شد، یک کپی برگردانده می‌شود
        var result = task != null ? CreateDeepCopy(task) : null;
        
        return Task.FromResult(result);
    }
    
    /// <inheritdoc />
    public Task<List<UserTaskInfo>> GetTasksAssignedToUserAsync(
        string userId, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            
        var tasks = _tasks.Values
            .Where(t => t.Assignee == userId)
            .Where(t => t.Status != UserTaskStatus.Completed && t.Status != UserTaskStatus.Cancelled)
            .OrderByDescending(t => t.CreatedAt)
            .ToList();
            
        // ایجاد کپی از تمام وظایف
        var result = tasks.Select(CreateDeepCopy).ToList();
        
        _logger.LogDebug("Retrieved {Count} tasks assigned to user {UserId}", 
            result.Count, userId);
            
        return Task.FromResult(result);
    }
    
    /// <inheritdoc />
    public Task<List<UserTaskInfo>> GetAvailableTasksForUserAsync(
        string userId, 
        ICollection<string>? userGroups = null, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            
        var tasks = _tasks.Values
            .Where(t => t.Status != UserTaskStatus.Completed && t.Status != UserTaskStatus.Cancelled)
            .Where(t => t.Assignee == userId || 
                       (t.Assignee == null && 
                        ((t.CandidateUsers != null && t.CandidateUsers.Contains(userId)) ||
                         (userGroups != null && t.CandidateGroups != null && 
                          t.CandidateGroups.Intersect(userGroups).Any()))))
            .OrderByDescending(t => t.CreatedAt)
            .ToList();
            
        // ایجاد کپی از تمام وظایف
        var result = tasks.Select(CreateDeepCopy).ToList();
        
        _logger.LogDebug("Retrieved {Count} available tasks for user {UserId}", 
            result.Count, userId);
            
        return Task.FromResult(result);
    }
    
    /// <inheritdoc />
    public Task<UserTaskSearchResult> SearchTasksAsync(
        UserTaskSearchOptions searchOptions, 
        CancellationToken cancellationToken = default)
    {
        if (searchOptions == null)
            throw new ArgumentNullException(nameof(searchOptions));
            
        // ابتدا تمام وظایف را با معیارهای جستجو فیلتر می‌کنیم
        var query = _tasks.Values.AsQueryable();
        
        // فیلتر بر اساس شناسه نمونه فرآیند
        if (!string.IsNullOrEmpty(searchOptions.ProcessInstanceId))
        {
            query = query.Where(t => t.ProcessInstanceId == searchOptions.ProcessInstanceId);
        }
        
        // فیلتر بر اساس مسئول انجام وظیفه
        if (!string.IsNullOrEmpty(searchOptions.AssigneeId))
        {
            query = query.Where(t => t.Assignee == searchOptions.AssigneeId);
        }
        
        // فیلتر بر اساس کاربر کاندیدا
        if (!string.IsNullOrEmpty(searchOptions.CandidateUserId))
        {
            query = query.Where(t => t.CandidateUsers != null && 
                                    t.CandidateUsers.Contains(searchOptions.CandidateUserId));
        }
        
        // فیلتر بر اساس گروه کاندیدا
        if (!string.IsNullOrEmpty(searchOptions.CandidateGroupId))
        {
            query = query.Where(t => t.CandidateGroups != null && 
                                    t.CandidateGroups.Contains(searchOptions.CandidateGroupId));
        }
        
        // فیلتر بر اساس وضعیت
        if (!searchOptions.IncludeCompleted)
        {
            query = query.Where(t => t.Status != UserTaskStatus.Completed && 
                                    t.Status != UserTaskStatus.Cancelled);
        }
        
        if (!searchOptions.IncludeActive)
        {
            query = query.Where(t => t.Status == UserTaskStatus.Completed || 
                                    t.Status == UserTaskStatus.Cancelled);
        }
        
        // فیلتر بر اساس عنوان
        if (!string.IsNullOrEmpty(searchOptions.TitleContains))
        {
            query = query.Where(t => t.TaskTitle != null && 
                                    t.TaskTitle.Contains(searchOptions.TitleContains, StringComparison.OrdinalIgnoreCase));
        }
        
        // فیلتر بر اساس تاریخ ایجاد
        if (searchOptions.CreatedAfter.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= searchOptions.CreatedAfter.Value);
        }
        
        if (searchOptions.CreatedBefore.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= searchOptions.CreatedBefore.Value);
        }
        
        // فیلتر بر اساس تاریخ سررسید
        if (searchOptions.DueAfter.HasValue)
        {
            query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value >= searchOptions.DueAfter.Value);
        }
        
        if (searchOptions.DueBefore.HasValue)
        {
            query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value <= searchOptions.DueBefore.Value);
        }
        
        // فیلتر بر اساس اولویت
        if (searchOptions.MinPriority.HasValue)
        {
            query = query.Where(t => t.Priority >= searchOptions.MinPriority.Value);
        }
        
        // مرتب‌سازی
        if (!string.IsNullOrEmpty(searchOptions.SortBy))
        {
            switch (searchOptions.SortBy.ToLowerInvariant())
            {
                case "createdat":
                    query = searchOptions.SortDescending 
                        ? query.OrderByDescending(t => t.CreatedAt) 
                        : query.OrderBy(t => t.CreatedAt);
                    break;
                    
                case "duedate":
                    query = searchOptions.SortDescending 
                        ? query.OrderByDescending(t => t.DueDate) 
                        : query.OrderBy(t => t.DueDate);
                    break;
                    
                case "priority":
                    query = searchOptions.SortDescending 
                        ? query.OrderByDescending(t => t.Priority) 
                        : query.OrderBy(t => t.Priority);
                    break;
                    
                case "title":
                    query = searchOptions.SortDescending 
                        ? query.OrderByDescending(t => t.TaskTitle) 
                        : query.OrderBy(t => t.TaskTitle);
                    break;
                    
                default:
                    query = query.OrderByDescending(t => t.CreatedAt);
                    break;
            }
        }
        else
        {
            // مرتب‌سازی پیش‌فرض
            query = query.OrderByDescending(t => t.CreatedAt);
        }
        
        // محاسبه‌ی تعداد کل
        var totalCount = query.Count();
        
        // محاسبه‌ی اندازه صفحه
        var pageSize = Math.Min(searchOptions.MaxResults, 100);
        
        // اعمال صفحه‌بندی
        var skip = Math.Max(0, searchOptions.Page) * pageSize;
        var tasksPage = query.Skip(skip).Take(pageSize).ToList();
        
        // ایجاد کپی از تمام وظایف
        var result = new UserTaskSearchResult
        {
            TotalCount = totalCount,
            Tasks = tasksPage.Select(CreateDeepCopy).ToList(),
            Page = searchOptions.Page,
            PageSize = pageSize,
            HasNextPage = (skip + pageSize) < totalCount
        };
        
        _logger.LogDebug("Search returned {Count} tasks out of {TotalCount} total", 
            result.Tasks.Count, result.TotalCount);
            
        return Task.FromResult(result);
    }
    
    /// <inheritdoc />
    public Task<int> DeleteCompletedTasksAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be null or empty", nameof(processInstanceId));
            
        var taskIdsToRemove = _tasks.Values
            .Where(t => t.ProcessInstanceId == processInstanceId)
            .Where(t => t.Status == UserTaskStatus.Completed || t.Status == UserTaskStatus.Cancelled)
            .Select(t => t.TaskId)
            .ToList();
            
        int removedCount = 0;
        
        foreach (var taskId in taskIdsToRemove)
        {
            if (_tasks.TryRemove(taskId, out _))
            {
                removedCount++;
            }
        }
        
        _logger.LogInformation("Deleted {Count} completed tasks for process {ProcessInstanceId}", 
            removedCount, processInstanceId);
            
        return Task.FromResult(removedCount);
    }
    
    /// <inheritdoc />
    public Task<int> DeleteAllTasksAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be null or empty", nameof(processInstanceId));
            
        var taskIdsToRemove = _tasks.Values
            .Where(t => t.ProcessInstanceId == processInstanceId)
            .Select(t => t.TaskId)
            .ToList();
            
        int removedCount = 0;
        
        foreach (var taskId in taskIdsToRemove)
        {
            if (_tasks.TryRemove(taskId, out _))
            {
                removedCount++;
            }
        }
        
        _logger.LogInformation("Deleted {Count} tasks for process {ProcessInstanceId}", 
            removedCount, processInstanceId);
            
        return Task.FromResult(removedCount);
    }
    
    /// <inheritdoc />
    public Task<bool> UpdateTaskStatusAsync(
        string taskId, 
        UserTaskStatus status, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));
            
        if (!_tasks.TryGetValue(taskId, out var task))
        {
            return Task.FromResult(false);
        }
        
        // به‌روزرسانی وضعیت
        task.Status = status;
        task.UpdatedAt = DateTime.UtcNow;
        
        // اگر وضعیت، تکمیل است، تاریخ تکمیل را نیز تنظیم می‌کنیم
        if (status == UserTaskStatus.Completed && !task.CompletedAt.HasValue)
        {
            task.CompletedAt = DateTime.UtcNow;
        }
        
        _logger.LogDebug("Updated status for task {TaskId} to {Status}", taskId, status);
        
        return Task.FromResult(true);
    }
    
    /// <summary>
    /// ایجاد یک کپی عمیق از وظیفه کاربری
    /// </summary>
    private UserTaskInfo CreateDeepCopy(UserTaskInfo source)
    {
        var copy = new UserTaskInfo
        {
            TaskId = source.TaskId,
            ProcessInstanceId = source.ProcessInstanceId,
            ProcessDefinitionId = source.ProcessDefinitionId,
            ProcessDefinitionKey = source.ProcessDefinitionKey,
            ElementId = source.ElementId,
            TaskTitle = source.TaskTitle,
            TaskDescription = source.TaskDescription,
            Assignee = source.Assignee,
            AssigneeName = source.AssigneeName,
            AssignedAt = source.AssignedAt,
            FormKey = source.FormKey,
            DueDate = source.DueDate,
            FollowUpDate = source.FollowUpDate,
            Priority = source.Priority,
            Status = source.Status,
            RemainingRetries = source.RemainingRetries,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            CompletedAt = source.CompletedAt,
            CompletedBy = source.CompletedBy,
            CompletedByName = source.CompletedByName,
            ErrorMessage = source.ErrorMessage,
            ErrorCode = source.ErrorCode,
            LastErrorAt = source.LastErrorAt
        };
        
        if (source.CandidateUsers != null)
        {
            copy.CandidateUsers = new List<string>(source.CandidateUsers);
        }
        
        if (source.CandidateGroups != null)
        {
            copy.CandidateGroups = new List<string>(source.CandidateGroups);
        }
        
        if (source.FormVariables != null)
        {
            copy.FormVariables = new Dictionary<string, object>(source.FormVariables);
        }
        
        if (source.FormData != null)
        {
            copy.FormData = new Dictionary<string, object>(source.FormData);
        }
        
        if (source.Comments != null)
        {
            copy.Comments = source.Comments.Select(c => new UserTaskComment
            {
                CommentId = c.CommentId,
                UserId = c.UserId,
                UserName = c.UserName,
                Text = c.Text,
                CreatedAt = c.CreatedAt,
                IsDeleted = c.IsDeleted,
                DeletedAt = c.DeletedAt
            }).ToList();
        }
        
        if (source.Tags != null)
        {
            copy.Tags = new List<string>(source.Tags);
        }
        
        if (source.Labels != null)
        {
            copy.Labels = new Dictionary<string, string>(source.Labels);
        }
        
        return copy;
    }
}