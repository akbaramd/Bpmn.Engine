using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// پیاده‌سازی سرویس مدیریت وظایف کاربری BPMN
/// </summary>
public class UserTaskService : IUserTaskService
{
    private readonly IUserTaskStore _taskStore;
    private readonly IEventBus _eventBus;
    private readonly ILogger<UserTaskService> _logger;
    
    /// <summary>
    /// ایجاد یک نمونه جدید از سرویس وظایف کاربری
    /// </summary>
    /// <param name="taskStore">مخزن ذخیره‌سازی وظایف</param>
    /// <param name="eventBus">سیستم انتشار رویدادها</param>
    /// <param name="logger">سیستم ثبت وقایع</param>
    public UserTaskService(
        IUserTaskStore taskStore,
        IEventBus eventBus,
        ILogger<UserTaskService> logger)
    {
        _taskStore = taskStore ?? throw new ArgumentNullException(nameof(taskStore));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc />
    public async Task<UserTaskInfo?> GetTaskByIdAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));
            
        _logger.LogDebug("Getting task by ID {TaskId}", taskId);
        return await _taskStore.GetTaskByIdAsync(taskId, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<List<UserTaskInfo>> GetTasksByProcessInstanceAsync(
        string processInstanceId, 
        bool includeCompleted = false, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be null or empty", nameof(processInstanceId));
            
        _logger.LogDebug("Getting tasks for process instance {ProcessInstanceId}, includeCompleted: {IncludeCompleted}", 
            processInstanceId, includeCompleted);
            
        return await _taskStore.GetTasksByProcessInstanceAsync(processInstanceId, includeCompleted, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<List<UserTaskInfo>> GetTasksAssignedToUserAsync(
        string userId, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            
        _logger.LogDebug("Getting tasks assigned to user {UserId}", userId);
        return await _taskStore.GetTasksAssignedToUserAsync(userId, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<List<UserTaskInfo>> GetAvailableTasksForUserAsync(
        string userId, 
        ICollection<string>? userGroups = null, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            
        _logger.LogDebug("Getting available tasks for user {UserId} with groups {UserGroups}", 
            userId, userGroups != null ? string.Join(", ", userGroups) : "none");
            
        return await _taskStore.GetAvailableTasksForUserAsync(userId, userGroups, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<UserTaskSearchResult> SearchTasksAsync(
        UserTaskSearchOptions searchOptions, 
        CancellationToken cancellationToken = default)
    {
        if (searchOptions == null)
            throw new ArgumentNullException(nameof(searchOptions));
            
        _logger.LogDebug("Searching tasks with options: ProcessInstanceId={ProcessInstanceId}, AssigneeId={AssigneeId}, IncludeCompleted={IncludeCompleted}", 
            searchOptions.ProcessInstanceId, searchOptions.AssigneeId, searchOptions.IncludeCompleted);
            
        return await _taskStore.SearchTasksAsync(searchOptions, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<UserTaskInfo> AssignTaskAsync(
        string taskId, 
        string userId, 
        string? userName = null, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));
            
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            
        _logger.LogInformation("Assigning task {TaskId} to user {UserId}", taskId, userId);
        
        var task = await _taskStore.GetTaskByIdAsync(taskId, cancellationToken);
        if (task == null)
            throw new KeyNotFoundException($"Task with ID {taskId} not found");
            
        if (task.Status == UserTaskStatus.Completed || task.Status == UserTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot assign task {taskId} because it is already {task.Status}");
            
        // Check if task is already assigned to this user
        if (task.Assignee == userId)
        {
            _logger.LogDebug("Task {TaskId} is already assigned to user {UserId}", taskId, userId);
            return task;
        }
        
        // Check if task is assigned to another user
        if (!string.IsNullOrEmpty(task.Assignee) && task.Assignee != userId)
            throw new InvalidOperationException($"Task {taskId} is already assigned to user {task.Assignee}");
            
        // Update task assignment
        task.Assignee = userId;
        task.AssigneeName = userName;
        task.AssignedAt = DateTime.UtcNow;
        task.Status = UserTaskStatus.Assigned;
        task.UpdatedAt = DateTime.UtcNow;
        
        // Publish assignment event
        await _eventBus.PublishAsync(new UserTaskAssignedEvent
        {
            ProcessInstanceId = task.ProcessInstanceId,
            UserTaskId = task.ElementId,
            UserId = userId,
            UserName = userName,
            Timestamp = DateTime.UtcNow
        }, cancellationToken);
        
        // Save changes
        return await _taskStore.UpdateTaskAsync(task, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<UserTaskInfo> UnassignTaskAsync(
        string taskId, 
        string userId, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));
            
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            
        _logger.LogInformation("Unassigning task {TaskId} from user {UserId}", taskId, userId);
        
        var task = await _taskStore.GetTaskByIdAsync(taskId, cancellationToken);
        if (task == null)
            throw new KeyNotFoundException($"Task with ID {taskId} not found");
            
        if (task.Status == UserTaskStatus.Completed || task.Status == UserTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot unassign task {taskId} because it is already {task.Status}");
            
        // Check if task is assigned to this user
        if (task.Assignee != userId)
            throw new InvalidOperationException($"Task {taskId} is not assigned to user {userId}");
            
        // Update task assignment
        task.Assignee = null;
        task.AssigneeName = null;
        task.AssignedAt = null;
        task.Status = UserTaskStatus.Active;
        task.UpdatedAt = DateTime.UtcNow;
        
        // Publish unassignment event
        await _eventBus.PublishAsync(new UserTaskUnassignedEvent
        {
            ProcessInstanceId = task.ProcessInstanceId,
            UserTaskId = task.ElementId,
            UserId = userId,
            Timestamp = DateTime.UtcNow
        }, cancellationToken);
        
        // Save changes
        return await _taskStore.UpdateTaskAsync(task, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<UserTaskInfo> CompleteTaskAsync(
        string taskId, 
        string userId, 
        Dictionary<string, object>? formData = null, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));
            
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            
        _logger.LogInformation("Completing task {TaskId} by user {UserId}", taskId, userId);
        
        var task = await _taskStore.GetTaskByIdAsync(taskId, cancellationToken);
        if (task == null)
            throw new KeyNotFoundException($"Task with ID {taskId} not found");
            
        if (task.Status == UserTaskStatus.Completed || task.Status == UserTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot complete task {taskId} because it is already {task.Status}");
            
        // Check if task is assigned to this user
        if (!string.IsNullOrEmpty(task.Assignee) && task.Assignee != userId)
            throw new InvalidOperationException($"Task {taskId} is assigned to {task.Assignee}, not to {userId}");
            
        // Update task completion
        task.Status = UserTaskStatus.Completed;
        task.CompletedAt = DateTime.UtcNow;
        task.CompletedBy = userId;
        task.FormData = formData;
        task.UpdatedAt = DateTime.UtcNow;
        
        // Publish completion event
        await _eventBus.PublishAsync(new UserTaskCompletedEvent
        {
            ProcessInstanceId = task.ProcessInstanceId,
            UserTaskId = task.ElementId,
            UserId = userId,
            FormData = formData,
            Timestamp = DateTime.UtcNow
        }, cancellationToken);
        
        // Save changes
        return await _taskStore.UpdateTaskAsync(task, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<UserTaskInfo> CreateTaskAsync(
        UserTaskInfo taskInfo, 
        CancellationToken cancellationToken = default)
    {
        if (taskInfo == null)
            throw new ArgumentNullException(nameof(taskInfo));
            
        if (string.IsNullOrEmpty(taskInfo.ProcessInstanceId))
            throw new ArgumentException("Process instance ID cannot be null or empty", nameof(taskInfo.ProcessInstanceId));
            
        if (string.IsNullOrEmpty(taskInfo.ElementId))
            throw new ArgumentException("Element ID cannot be null or empty", nameof(taskInfo.ElementId));
            
        _logger.LogInformation("Creating new user task for process {ProcessInstanceId}, element {ElementId}", 
            taskInfo.ProcessInstanceId, taskInfo.ElementId);
            
        // Set default values
        if (string.IsNullOrEmpty(taskInfo.TaskId))
            taskInfo.TaskId = Guid.NewGuid().ToString();
            
        taskInfo.CreatedAt = DateTime.UtcNow;
        taskInfo.UpdatedAt = DateTime.UtcNow;
        taskInfo.Status = UserTaskStatus.Active;
        
        // Publish creation event
        await _eventBus.PublishAsync(new UserTaskCreatedEvent
        {
            ProcessInstanceId = taskInfo.ProcessInstanceId,
            UserTaskId = taskInfo.ElementId,
            TaskTitle = taskInfo.TaskTitle,
            TaskDescription = taskInfo.TaskDescription,
            FormKey = taskInfo.FormKey,
            CandidateUsers = taskInfo.CandidateUsers,
            CandidateGroups = taskInfo.CandidateGroups,
            Assignee = taskInfo.Assignee,
            DueDate = taskInfo.DueDate,
            Priority = taskInfo.Priority,
            Timestamp = DateTime.UtcNow
        }, cancellationToken);
        
        // Save task
        return await _taskStore.SaveTaskAsync(taskInfo, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<UserTaskInfo> SetDueDateAsync(
        string taskId, 
        DateTime dueDate, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));
            
        _logger.LogInformation("Setting due date for task {TaskId} to {DueDate}", taskId, dueDate);
        
        var task = await _taskStore.GetTaskByIdAsync(taskId, cancellationToken);
        if (task == null)
            throw new KeyNotFoundException($"Task with ID {taskId} not found");
            
        if (task.Status == UserTaskStatus.Completed || task.Status == UserTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot update due date for task {taskId} because it is already {task.Status}");
            
        // Update due date
        task.DueDate = dueDate;
        task.UpdatedAt = DateTime.UtcNow;
        
        // Publish due date event if task is overdue
        if (dueDate <= DateTime.UtcNow)
        {
            await _eventBus.PublishAsync(new UserTaskDueEvent
            {
                ProcessInstanceId = task.ProcessInstanceId,
                UserTaskId = task.ElementId,
                DueDate = dueDate,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
        }
        
        // Save changes
        return await _taskStore.UpdateTaskAsync(task, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<string> AddCommentAsync(
        string taskId, 
        string userId, 
        string userName, 
        string commentText, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));
            
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            
        if (string.IsNullOrEmpty(commentText))
            throw new ArgumentException("Comment text cannot be null or empty", nameof(commentText));
            
        _logger.LogInformation("Adding comment to task {TaskId} by user {UserId}", taskId, userId);
        
        var task = await _taskStore.GetTaskByIdAsync(taskId, cancellationToken);
        if (task == null)
            throw new KeyNotFoundException($"Task with ID {taskId} not found");
            
        // Create new comment
        var comment = new UserTaskComment
        {
            CommentId = Guid.NewGuid().ToString(),
            UserId = userId,
            UserName = userName,
            Text = commentText,
            CreatedAt = DateTime.UtcNow
        };
        
        // Add comment to task
        if (task.Comments == null)
            task.Comments = new List<UserTaskComment>();
            
        task.Comments.Add(comment);
        task.UpdatedAt = DateTime.UtcNow;
        
        // Publish comment event
        await _eventBus.PublishAsync(new TaskCommentAddedEvent
        {
            ProcessInstanceId = task.ProcessInstanceId,
            UserTaskId = task.ElementId,
            CommentId = comment.CommentId,
            UserId = userId,
            UserName = userName,
            CommentText = commentText,
            Timestamp = DateTime.UtcNow
        }, cancellationToken);
        
        // Save changes
        await _taskStore.UpdateTaskAsync(task, cancellationToken);
        
        return comment.CommentId;
    }
    
    /// <inheritdoc />
    public async Task<UserTaskInfo> SetPriorityAsync(
        string taskId, 
        int priority, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));
            
        if (priority < 0 || priority > 100)
            throw new ArgumentOutOfRangeException(nameof(priority), "Priority must be between 0 and 100");
            
        _logger.LogInformation("Setting priority for task {TaskId} to {Priority}", taskId, priority);
        
        var task = await _taskStore.GetTaskByIdAsync(taskId, cancellationToken);
        if (task == null)
            throw new KeyNotFoundException($"Task with ID {taskId} not found");
            
        if (task.Status == UserTaskStatus.Completed || task.Status == UserTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot update priority for task {taskId} because it is already {task.Status}");
            
        // Update priority
        int oldPriority = task.Priority;
        task.Priority = priority;
        task.UpdatedAt = DateTime.UtcNow;
        
        // Publish priority change event
        await _eventBus.PublishAsync(new UserTaskPriorityChangedEvent
        {
            ProcessInstanceId = task.ProcessInstanceId,
            UserTaskId = task.ElementId,
            OldPriority = oldPriority,
            NewPriority = priority,
            Timestamp = DateTime.UtcNow
        }, cancellationToken);
        
        // Save changes
        return await _taskStore.UpdateTaskAsync(task, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<UserTaskInfo> UpdateFormVariablesAsync(
        string taskId, 
        Dictionary<string, object> formVariables, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));
            
        if (formVariables == null)
            throw new ArgumentNullException(nameof(formVariables));
            
        _logger.LogInformation("Updating form variables for task {TaskId}", taskId);
        
        var task = await _taskStore.GetTaskByIdAsync(taskId, cancellationToken);
        if (task == null)
            throw new KeyNotFoundException($"Task with ID {taskId} not found");
            
        if (task.Status == UserTaskStatus.Completed || task.Status == UserTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot update form variables for task {taskId} because it is already {task.Status}");
            
        // Update form variables
        task.FormVariables = new Dictionary<string, object>(formVariables);
        task.UpdatedAt = DateTime.UtcNow;
        
        // Save changes
        return await _taskStore.UpdateTaskAsync(task, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<UserTaskInfo> SetCandidateGroupsAsync(
        string taskId, 
        ICollection<string> candidateGroups, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));
            
        if (candidateGroups == null || candidateGroups.Count == 0)
            throw new ArgumentException("Candidate groups cannot be null or empty", nameof(candidateGroups));
            
        _logger.LogInformation("Setting candidate groups for task {TaskId}", taskId);
        
        var task = await _taskStore.GetTaskByIdAsync(taskId, cancellationToken);
        if (task == null)
            throw new KeyNotFoundException($"Task with ID {taskId} not found");
            
        if (task.Status == UserTaskStatus.Completed || task.Status == UserTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot update candidate groups for task {taskId} because it is already {task.Status}");
            
        // Update candidate groups
        task.CandidateGroups = new List<string>(candidateGroups);
        task.UpdatedAt = DateTime.UtcNow;
        
        // Save changes
        return await _taskStore.UpdateTaskAsync(task, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<UserTaskInfo> SetCandidateUsersAsync(
        string taskId, 
        ICollection<string> candidateUsers, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(taskId))
            throw new ArgumentException("Task ID cannot be null or empty", nameof(taskId));
            
        if (candidateUsers == null || candidateUsers.Count == 0)
            throw new ArgumentException("Candidate users cannot be null or empty", nameof(candidateUsers));
            
        _logger.LogInformation("Setting candidate users for task {TaskId}", taskId);
        
        var task = await _taskStore.GetTaskByIdAsync(taskId, cancellationToken);
        if (task == null)
            throw new KeyNotFoundException($"Task with ID {taskId} not found");
            
        if (task.Status == UserTaskStatus.Completed || task.Status == UserTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot update candidate users for task {taskId} because it is already {task.Status}");
            
        // Update candidate users
        task.CandidateUsers = new List<string>(candidateUsers);
        task.UpdatedAt = DateTime.UtcNow;
        
        // Save changes
        return await _taskStore.UpdateTaskAsync(task, cancellationToken);
    }
} 