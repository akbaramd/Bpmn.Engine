using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// پردازشگر جریان رویدادهای فرآیند BPMN
/// این پردازشگر با استفاده از معماری Event Sourcing، وضعیت فرآیند را بر اساس رویدادها بازسازی می‌کند
/// </summary>
public class BpmnProcessStreamProcessor : AbstractStreamProcessor
{
    private readonly IStateStore _stateStore;
    private readonly IUserTaskService _userTaskService;
    private readonly ILogger<BpmnProcessStreamProcessor> _logger;

    /// <summary>
    /// ایجاد یک نمونه جدید از پردازشگر فرآیند BPMN
    /// </summary>
    /// <param name="eventStore">مخزن رویدادها</param>
    /// <param name="eventBus">گذرگاه رویداد</param>
    /// <param name="stateStore">مخزن وضعیت</param>
    /// <param name="userTaskService">سرویس وظایف کاربری</param>
    /// <param name="logger">سیستم ثبت وقایع</param>
    public BpmnProcessStreamProcessor(
        IEventStore eventStore,
        IEventBus eventBus,
        IStateStore stateStore,
        IUserTaskService userTaskService,
        ILogger<BpmnProcessStreamProcessor> logger)
        : base("BpmnProcessStreamProcessor", eventStore, eventBus, logger)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _userTaskService = userTaskService ?? throw new ArgumentNullException(nameof(userTaskService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // ثبت رویدادهای فرآیند
        RegisterInterestedEventType<ProcessInstanceCreating>();
        RegisterInterestedEventType<ProcessInstanceCreated>();
        RegisterInterestedEventType<ProcessInstanceStarting>();
        RegisterInterestedEventType<ProcessInstanceStarted>();
        RegisterInterestedEventType<ProcessInstanceCompleting>();
        RegisterInterestedEventType<ProcessCompletedEvent>();
        RegisterInterestedEventType<ProcessInstanceDeleting>();
        RegisterInterestedEventType<ProcessInstanceDeleted>();
        RegisterInterestedEventType<ProcessInstanceSuspending>();
        RegisterInterestedEventType<ProcessInstanceSuspended>();
        RegisterInterestedEventType<ProcessInstanceResuming>();
        RegisterInterestedEventType<ProcessInstanceResumed>();
        RegisterInterestedEventType<VariableUpdating>();
        RegisterInterestedEventType<VariableUpdated>();
        
        // ثبت رویدادهای المان
        RegisterInterestedEventType<ElementActivating>();
        RegisterInterestedEventType<ElementActivated>();
        RegisterInterestedEventType<ElementCompleting>();
        RegisterInterestedEventType<ElementCompleted>();
        RegisterInterestedEventType<ElementFailed>();
        RegisterInterestedEventType<ElementTerminating>();
        RegisterInterestedEventType<ElementTerminated>();
        
        // ثبت رویدادهای کار
        RegisterInterestedEventType<JobCreatedEvent>();
        RegisterInterestedEventType<JobActivatedEvent>();
        RegisterInterestedEventType<JobCompletedEvent>();
        RegisterInterestedEventType<JobFailedEvent>();
        RegisterInterestedEventType<JobTimeoutEvent>();
        RegisterInterestedEventType<JobErrorEvent>();
        
        // ثبت تمام رویدادهای وظایف کاربری
        RegisterInterestedEventType<UserTaskCreatedEvent>();
        RegisterInterestedEventType<UserTaskAssignedEvent>();
        RegisterInterestedEventType<UserTaskUnassignedEvent>();
        RegisterInterestedEventType<UserTaskCompletedEvent>();
        RegisterInterestedEventType<TaskCommentAddedEvent>();
        RegisterInterestedEventType<UserTaskDueEvent>();
        RegisterInterestedEventType<UserTaskPriorityChangedEvent>();
        RegisterInterestedEventType<UserTaskSubmittedEvent>();
        RegisterInterestedEventType<UserTaskClaimedEvent>();
    }

    /// <inheritdoc />
    protected override async Task HandleEventAsync(IBpmnEvent @event)
    {
        var processInstanceId = @event.ProcessInstanceId;
        
        // بازیابی وضعیت فعلی فرآیند (یا ایجاد یک وضعیت جدید)
        var (state, version) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(processInstanceId);
        state ??= new BpmnProcessState
        {
            ProcessInstanceId = processInstanceId,
            Status = ProcessStatus.Created,
            Variables = new Dictionary<string, object>(),
            ActiveElements = new HashSet<string>(),
            CompletedElements = new HashSet<string>(),
            History = new List<HistoryEntry>()
        };

        // بروزرسانی وضعیت بر اساس نوع رویداد و قصد آن
        switch (@event)
        {
            // رویدادهای فرآیند
            case ProcessInstanceCreating creating:
                ApplyProcessInstanceCreating(state, creating);
                break;
            
            case ProcessInstanceCreated created:
                // در حالت ساده، نیازی به انجام کار اضافی نیست
                // ولی می‌توان اعمال تنظیمات اولیه را اینجا انجام داد
                state.ProcessDefinitionId = created.ProcessDefinitionId;
                if (created.Variables != null)
                {
                    foreach (var variable in created.Variables)
                    {
                        state.Variables[variable.Key] = variable.Value;
                    }
                }
                break;
            
            case ProcessInstanceStarting starting:
                // تنظیم وضعیت به شروع شده و ثبت رویداد شروع
                state.Status = ProcessStatus.Running;
                state.ActiveElements.Add(starting.StartEventId);
                break;
            
            case ProcessInstanceStarted started:
                state.Status = ProcessStatus.Running;
                state.StartedAt = @event.Timestamp;
                break;
            
            case ProcessInstanceCompleting completing:
                // آماده‌سازی برای تکمیل فرآیند
                state.Status = ProcessStatus.Completed;
                if (completing.FinalVariables != null)
                {
                    foreach (var variable in completing.FinalVariables)
                    {
                        state.Variables[variable.Key] = variable.Value;
                    }
                }
                break;
            
            case ProcessCompletedEvent completed:
                // تکمیل فرآیند
                state.Status = ProcessStatus.Completed;
                state.CompletedAt = @event.Timestamp;
                if (completed.EndEventId != null)
                {
                    state.CompletedElements.Add(completed.EndEventId);
                }
                // تکمیل همه المان‌های فعال
                foreach (var activeElement in state.ActiveElements.ToList())
                {
                    state.CompletedElements.Add(activeElement);
                }
                state.ActiveElements.Clear();
                break;
            
            case ProcessInstanceDeleting deleting:
                // آماده‌سازی برای حذف فرآیند
                state.DeletionReason = deleting.Reason;
                break;
            
            case ProcessInstanceDeleted deleted:
                // حذف فرآیند
                state.Status = ProcessStatus.Deleted;
                state.DeletedAt = @event.Timestamp;
                state.ActiveElements.Clear();
                break;
            
            case ProcessInstanceSuspending suspending:
                // آماده‌سازی برای تعلیق فرآیند
                state.SuspensionReason = suspending.Reason;
                break;
            
            case ProcessInstanceSuspended suspended:
                // تعلیق فرآیند
                state.Status = ProcessStatus.Suspended;
                state.SuspendedAt = @event.Timestamp;
                break;
            
            case ProcessInstanceResuming resuming:
                // آماده‌سازی برای ازسرگیری فرآیند
                break;
            
            case ProcessInstanceResumed resumed:
                // ازسرگیری فرآیند
                state.Status = ProcessStatus.Running;
                state.ResumedAt = @event.Timestamp;
                break;
            
            case VariableUpdating variableUpdating:
                // بروزرسانی متغیر
                state.Variables[variableUpdating.VariableName] = variableUpdating.Value;
                break;
            
            case VariableUpdated variableUpdated:
                // تکمیل بروزرسانی متغیر
                break;
                
            // رویدادهای المان
            case ElementActivating activating:
                // المان در حال فعال‌سازی است
                break;
                
            case ElementActivated activated:
                // المان فعال شده است
                ApplyElementActivated(state, activated);
                break;
                
            case ElementCompleting completing:
                // المان در حال تکمیل است
                if (completing.UpdatedVariables != null)
                {
                    foreach (var variable in completing.UpdatedVariables)
                    {
                        state.Variables[variable.Key] = variable.Value;
                    }
                }
                break;
                
            case ElementCompleted completed:
                // المان تکمیل شده است
                ApplyElementCompleted(state, completed);
                break;
                
            case ElementFailed failed:
                // المان با خطا مواجه شده است
                ApplyElementFailed(state, failed);
                break;
                
            case ElementTerminating terminating:
                // المان در حال خاتمه است
                break;
                
            case ElementTerminated terminated:
                // المان خاتمه یافته است
                ApplyElementTerminated(state, terminated);
                break;
                
            // رویدادهای کار
            case JobCreatedEvent jobCreated:
                // کار ایجاد شده است
                ApplyJobCreated(state, jobCreated);
                break;
                
            case JobActivatedEvent jobActivated:
                // کار فعال شده است
                ApplyJobActivated(state, jobActivated);
                break;
                
            case JobCompletedEvent jobCompleted:
                // کار تکمیل شده است
                ApplyJobCompleted(state, jobCompleted);
                break;
                
            case JobFailedEvent jobFailed:
                // کار با شکست مواجه شده است
                ApplyJobFailed(state, jobFailed);
                break;
                
            case JobTimeoutEvent jobTimeout:
                // زمان کار به پایان رسیده است
                ApplyJobTimeout(state, jobTimeout);
                break;
                
            case JobErrorEvent jobError:
                // کار با خطا مواجه شده است
                ApplyJobError(state, jobError);
                break;
                
            // رویدادهای وظیفه کاربر
            case Events.UserTaskCreatedEvent userTaskCreated:
                // وظیفه کاربر ایجاد شده است
                await ApplyUserTaskCreated(state, userTaskCreated);
                break;
                
            case Events.UserTaskClaimedEvent userTaskClaimed:
                // وظیفه کاربر ادعا شده است
                ApplyUserTaskClaimed(state, userTaskClaimed);
                break;
                
            case Events.UserTaskSubmittedEvent userTaskSubmitted:
                // وظیفه کاربر ارسال شده است
                ApplyUserTaskSubmitted(state, userTaskSubmitted);
                break;

            case Events.UserTaskAssignedEvent userTaskAssigned:
                // وظیفه کاربر تخصیص داده شده است
                ApplyUserTaskAssigned(state, userTaskAssigned);
                break;

            case Events.UserTaskUnassignedEvent userTaskUnassigned:
                // تخصیص وظیفه کاربر لغو شده است
                ApplyUserTaskUnassigned(state, userTaskUnassigned);
                break;

            case Events.UserTaskCompletedEvent userTaskCompleted:
                // وظیفه کاربر تکمیل شده است
                ApplyUserTaskCompleted(state, userTaskCompleted);
                break;

            case Events.TaskCommentAddedEvent taskCommentAdded:
                // کامنت به وظیفه کاربر اضافه شده است
                ApplyTaskCommentAdded(state, taskCommentAdded);
                break;

            case Events.UserTaskDueEvent userTaskDue:
                // زمان سررسید وظیفه کاربر فرا رسیده است
                ApplyUserTaskDue(state, userTaskDue);
                break;

            case Events.UserTaskPriorityChangedEvent userTaskPriorityChanged:
                // اولویت وظیفه کاربر تغییر کرده است
                ApplyUserTaskPriorityChanged(state, userTaskPriorityChanged);
                break;
        }

        // افزودن رویداد به تاریخچه
        state.History.Add(new HistoryEntry
        {
            EventId = @event.EventId,
            EventType = @event.EventType,
            Intent = @event.Intent,
            Timestamp = @event.Timestamp,
            UserId = @event.UserId
        });

        // ذخیره وضعیت آپدیت شده
        await _stateStore.SaveStateAsync(processInstanceId, state, version > 0 ? version : null);
    }

    private void ApplyProcessInstanceCreating(BpmnProcessState state, ProcessInstanceCreating @event)
    {
        state.Status = ProcessStatus.Created;
        state.ProcessDefinitionId = @event.ProcessDefinitionId;
        state.DeploymentKey = @event.DeploymentKey;
        state.DefinitionXml = @event.DefinitionXml;
        state.CreatedAt = @event.Timestamp;
        
        // اضافه کردن متغیرهای اولیه
        if (@event.InitialVariables != null)
        {
            foreach (var variable in @event.InitialVariables)
            {
                state.Variables[variable.Key] = variable.Value;
            }
        }
    }

    private void ApplyElementActivated(BpmnProcessState state, ElementActivated @event)
    {
        // افزودن المان به لیست المان‌های فعال
        state.ActiveElements.Add(@event.ElementId);
        
        // بسته به نوع المان، ممکن است منطق خاصی نیاز باشد
        switch (@event.ElementType)
        {
            case "bpmn:UserTask":
                if (state.Tasks == null)
                    state.Tasks = new Dictionary<string, TaskInfo>();
                
                // وظیفه کاربر به طور جداگانه توسط رویدادهای وظیفه مدیریت می‌شود
                break;
                
            case "bpmn:ServiceTask":
                if (state.Tasks == null)
                    state.Tasks = new Dictionary<string, TaskInfo>();
                
                // وظیفه سرویس به طور جداگانه توسط رویدادهای کار مدیریت می‌شود
                break;
        }
    }

    private void ApplyElementCompleted(BpmnProcessState state, ElementCompleted @event)
    {
        // حذف از المان‌های فعال و افزودن به المان‌های تکمیل شده
        state.ActiveElements.Remove(@event.ElementId);
        state.CompletedElements.Add(@event.ElementId);
    }

    private void ApplyElementFailed(BpmnProcessState state, ElementFailed @event)
    {
        if (@event.HasErrorBoundaryEvent)
        {
            // اگر رویداد مرزی خطا وجود دارد، المان فعلی باید غیرفعال شود
            // و رویداد مرزی باید فعال شود
            state.ActiveElements.Remove(@event.ElementId);
            state.FailedElements ??= new HashSet<string>();
            state.FailedElements.Add(@event.ElementId);
            
            if (!string.IsNullOrEmpty(@event.ErrorBoundaryEventId))
                state.ActiveElements.Add(@event.ErrorBoundaryEventId);
        }
        else
        {
            // اگر رویداد مرزی خطا وجود ندارد، فرآیند باید به حالت خطا برود
            state.Status = ProcessStatus.Error;
            state.ErrorMessage = @event.ErrorMessage;
            state.ErrorCode = @event.ErrorCode;
        }
    }

    private void ApplyElementTerminated(BpmnProcessState state, ElementTerminated @event)
    {
        // حذف از المان‌های فعال و افزودن به المان‌های خاتمه یافته
        state.ActiveElements.Remove(@event.ElementId);
        state.TerminatedElements ??= new HashSet<string>();
        state.TerminatedElements.Add(@event.ElementId);
    }

    private void ApplyJobCreated(BpmnProcessState state, JobCreatedEvent @event)
    {
        // ایجاد یا بروزرسانی کار
        state.Jobs ??= new Dictionary<string, JobInfo>();
        
        state.Jobs[@event.JobId] = new JobInfo
        {
            JobId = @event.JobId,
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            JobType = @event.JobType,
            Retries = @event.Retries,
            Deadline = @event.Deadline,
            Status = JobStatus.Created,
            CustomHeaders = @event.CustomHeaders,
            CreatedAt = @event.Timestamp
        };
    }

    private void ApplyJobActivated(BpmnProcessState state, JobActivatedEvent @event)
    {
        if (state.Jobs != null && state.Jobs.TryGetValue(@event.JobId, out var jobInfo))
        {
            jobInfo.Status = JobStatus.Activated;
            jobInfo.WorkerId = @event.WorkerId;
            jobInfo.Deadline = @event.Deadline;
            jobInfo.ActivatedAt = @event.Timestamp;
        }
    }

    private void ApplyJobCompleted(BpmnProcessState state, JobCompletedEvent @event)
    {
        if (state.Jobs != null && state.Jobs.TryGetValue(@event.JobId, out var jobInfo))
        {
            jobInfo.Status = JobStatus.Completed;
            jobInfo.Result = @event.Variables;
            jobInfo.CompletedAt = @event.Timestamp;
        }
        
        // در اینجا باید همچنین المان مرتبط با این کار تکمیل شود
        // اما در مدل فعلی این ارتباط از طریق رویدادهای ElementCompleted مدیریت می‌شود
    }

    private void ApplyJobFailed(BpmnProcessState state, JobFailedEvent @event)
    {
        if (state.Jobs != null && state.Jobs.TryGetValue(@event.JobId, out var jobInfo))
        {
            jobInfo.Status = JobStatus.Failed;
            jobInfo.ErrorMessage = @event.ErrorMessage;
            jobInfo.Retries = @event.RemainingRetries;
            jobInfo.RetryBackOff = @event.RetryBackOff;
            jobInfo.FailedAt = @event.Timestamp;
        }
    }

    private void ApplyJobTimeout(BpmnProcessState state, JobTimeoutEvent @event)
    {
        if (state.Jobs != null && state.Jobs.TryGetValue(@event.JobId, out var jobInfo))
        {
            jobInfo.Status = JobStatus.Timeout;
            jobInfo.TimedOutAt = @event.Timestamp;
        }
    }

    private void ApplyJobError(BpmnProcessState state, JobErrorEvent @event)
    {
        if (state.Jobs != null && state.Jobs.TryGetValue(@event.JobId, out var jobInfo))
        {
            jobInfo.Status = JobStatus.Error;
            jobInfo.ErrorCode = @event.ErrorCode;
            jobInfo.ErrorMessage = @event.ErrorMessage;
            jobInfo.ErrorAt = @event.Timestamp;
        }
    }
    
    private async Task ApplyUserTaskCreated(BpmnProcessState state, Events.UserTaskCreatedEvent @event)
    {
        // ایجاد یا بروزرسانی وظیفه کاربر در حالت
        state.Tasks ??= new Dictionary<string, TaskInfo>();
        
        // ایجاد TaskInfo برای وضعیت داخلی
        state.Tasks[@event.UserTaskId] = new TaskInfo
        {
            TaskId = @event.UserTaskId,
            TaskType = "UserTask",
            TaskTitle = @event.TaskTitle,
            TaskDescription = @event.TaskDescription,
            Assignee = @event.Assignee,
            CandidateGroups = @event.CandidateGroups,
            CandidateUsers = @event.CandidateUsers,
            FormKey = @event.FormKey,
            DueDate = @event.DueDate,
            FormVariables = @event.FormVariables,
            CreatedAt = @event.Timestamp,
            Status = TaskStatus.Active
        };
        
        // افزودن به عناصر فعال
        state.ActiveElements.Add(@event.UserTaskId);
        
    }
    
    private void ApplyUserTaskClaimed(BpmnProcessState state, Events.UserTaskClaimedEvent @event)
    {
        if (state.Tasks != null && state.Tasks.TryGetValue(@event.UserTaskId, out var taskInfo))
        {
            // بروزرسانی وضعیت وظیفه
            taskInfo.Assignee = @event.AssigneeId;
        }
    }
    
    private void ApplyUserTaskSubmitted(BpmnProcessState state, Events.UserTaskSubmittedEvent @event)
    {
        if (state.Tasks != null && state.Tasks.TryGetValue(@event.UserTaskId, out var taskInfo))
        {
            // بروزرسانی وضعیت وظیفه
            taskInfo.Status = TaskStatus.Completed;
            taskInfo.CompletedAt = @event.Timestamp;
            taskInfo.FormData = @event.FormData;
            
            // بروزرسانی متغیرهای فرآیند
            if (@event.OutputVariables != null)
            {
                foreach (var variable in @event.OutputVariables)
                {
                    state.Variables[variable.Key] = variable.Value;
                }
            }
        }
    }

    private void ApplyUserTaskAssigned(BpmnProcessState state, Events.UserTaskAssignedEvent @event)
    {
        if (state.Tasks != null && state.Tasks.TryGetValue(@event.UserTaskId, out var taskInfo))
        {
            // بروزرسانی اطلاعات تخصیص وظیفه
            taskInfo.Assignee = @event.UserId;
            
            // اضافه کردن اطلاعات اختیاری
            if (taskInfo is TaskInfo task)
            {
                // اطلاعات اضافی مانند نام کاربر
                string userName = @event.UserName ?? @event.UserId;
                
                // افزودن به تاریخچه رویداد
                state.History.Add(new HistoryEntry
                {
                    EventId = @event.EventId,
                    EventType = "UserTaskAssigned",
                    Timestamp = @event.Timestamp,
                    UserId = @event.UserId
                });
            }
        }
    }

    private void ApplyUserTaskUnassigned(BpmnProcessState state, Events.UserTaskUnassignedEvent @event)
    {
        if (state.Tasks != null && state.Tasks.TryGetValue(@event.UserTaskId, out var taskInfo))
        {
            // حذف تخصیص وظیفه
            taskInfo.Assignee = null;
            
            // افزودن به تاریخچه رویداد
            state.History.Add(new HistoryEntry
            {
                EventId = @event.EventId,
                EventType = "UserTaskUnassigned",
                Timestamp = @event.Timestamp,
                UserId = @event.UserId
            });
        }
    }

    private void ApplyUserTaskCompleted(BpmnProcessState state, Events.UserTaskCompletedEvent @event)
    {
        if (state.Tasks != null && state.Tasks.TryGetValue(@event.UserTaskId, out var taskInfo))
        {
            // تکمیل وظیفه
            taskInfo.Status = TaskStatus.Completed;
            taskInfo.CompletedAt = @event.Timestamp;
            taskInfo.FormData = @event.FormData;
            
            // بروزرسانی متغیرهای فرآیند با داده‌های فرم
            if (@event.FormData != null)
            {
                foreach (var data in @event.FormData)
                {
                    state.Variables[data.Key] = data.Value;
                }
            }
            
            // حذف از عناصر فعال و افزودن به عناصر تکمیل شده
            // توجه: تصمیم‌گیری در مورد حذف از ActiveElements به واسطه رویداد ElementCompleted انجام می‌شود
            // اما برای اطمینان در اینجا نیز بررسی می‌کنیم
            if (state.ActiveElements.Contains(@event.UserTaskId))
            {
                state.ActiveElements.Remove(@event.UserTaskId);
                state.CompletedElements.Add(@event.UserTaskId);
            }
        }
    }

    private void ApplyTaskCommentAdded(BpmnProcessState state, Events.TaskCommentAddedEvent @event)
    {
        // این رویداد در مدل وضعیت فعلی ما به صورت مستقیم ذخیره نمی‌شود
        // در پیاده‌سازی کامل‌تر می‌توان وضعیت کامنت‌های وظیفه را نیز ذخیره کرد
        
        // افزودن به تاریخچه رویداد
        state.History.Add(new HistoryEntry
        {
            EventId = @event.EventId,
            EventType = "TaskCommentAdded",
            Timestamp = @event.Timestamp,
            UserId = @event.UserId
        });
    }

    private void ApplyUserTaskDue(BpmnProcessState state, Events.UserTaskDueEvent @event)
    {
        if (state.Tasks != null && state.Tasks.TryGetValue(@event.UserTaskId, out var taskInfo))
        {
            // بروزرسانی زمان سررسید
            taskInfo.DueDate = @event.DueDate;
            
            // افزودن به تاریخچه رویداد
            state.History.Add(new HistoryEntry
            {
                EventId = @event.EventId,
                EventType = "UserTaskDue",
                Timestamp = @event.Timestamp,
                UserId = @event.UserId
            });
        }
    }

    private void ApplyUserTaskPriorityChanged(BpmnProcessState state, Events.UserTaskPriorityChangedEvent @event)
    {
        // در مدل فعلی ما اولویت به صورت مستقیم ذخیره نمی‌شود
        // می‌توان در آینده فیلد Priority را به TaskInfo اضافه کرد
        
        // افزودن به تاریخچه رویداد
        state.History.Add(new HistoryEntry
        {
            EventId = @event.EventId,
            EventType = "UserTaskPriorityChanged",
            Timestamp = @event.Timestamp,
            UserId = @event.UserId
        });
    }
}

/// <summary>
/// وضعیت نمونه فرآیند BPMN
/// </summary>
public class BpmnProcessState
{
    /// <summary>
    /// شناسه نمونه فرآیند
    /// </summary>
    public required string ProcessInstanceId { get; set; }
    
    /// <summary>
    /// شناسه تعریف فرآیند
    /// </summary>
    public string? ProcessDefinitionId { get; set; }
    
    /// <summary>
    /// کلید انتشار
    /// </summary>
    public string? DeploymentKey { get; set; }
    
    /// <summary>
    /// XML تعریف BPMN
    /// </summary>
    public string? DefinitionXml { get; set; }
    
    /// <summary>
    /// وضعیت فرآیند
    /// </summary>
    public ProcessStatus Status { get; set; }
    
    /// <summary>
    /// زمان ایجاد
    /// </summary>
    public DateTime? CreatedAt { get; set; }
    
    /// <summary>
    /// زمان شروع
    /// </summary>
    public DateTime? StartedAt { get; set; }
    
    /// <summary>
    /// زمان تکمیل
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// زمان حذف
    /// </summary>
    public DateTime? DeletedAt { get; set; }
    
    /// <summary>
    /// دلیل حذف
    /// </summary>
    public string? DeletionReason { get; set; }
    
    /// <summary>
    /// زمان توقف
    /// </summary>
    public DateTime? SuspendedAt { get; set; }
    
    /// <summary>
    /// دلیل توقف
    /// </summary>
    public string? SuspensionReason { get; set; }
    
    /// <summary>
    /// زمان ازسرگیری
    /// </summary>
    public DateTime? ResumedAt { get; set; }
    
    /// <summary>
    /// کد خطا (در حالت خطا)
    /// </summary>
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// پیام خطا (در حالت خطا)
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// متغیرهای فرآیند
    /// </summary>
    public Dictionary<string, object> Variables { get; set; } = new();
    
    /// <summary>
    /// عناصر فعال
    /// </summary>
    public HashSet<string> ActiveElements { get; set; } = new();
    
    /// <summary>
    /// عناصر تکمیل شده
    /// </summary>
    public HashSet<string> CompletedElements { get; set; } = new();
    
    /// <summary>
    /// عناصر خطا دار
    /// </summary>
    public HashSet<string>? FailedElements { get; set; }
    
    /// <summary>
    /// عناصر خاتمه یافته
    /// </summary>
    public HashSet<string>? TerminatedElements { get; set; }
    
    /// <summary>
    /// وظایف
    /// </summary>
    public Dictionary<string, TaskInfo>? Tasks { get; set; }
    
    /// <summary>
    /// کارها
    /// </summary>
    public Dictionary<string, JobInfo>? Jobs { get; set; }
    
    /// <summary>
    /// اطلاعات گیت‌وی‌ها
    /// </summary>
    public Dictionary<string, GatewayInfo>? GatewayInfo { get; set; }
    
    /// <summary>
    /// اطلاعات گیت‌وی‌های مبتنی بر رویداد فعال
    /// </summary>
    public Dictionary<string, EventBasedGatewayInfo>? EventBasedGateways { get; set; }
    
    /// <summary>
    /// تاریخچه رویدادها
    /// </summary>
    public List<HistoryEntry> History { get; set; } = new();
}

/// <summary>
/// وضعیت فرآیند
/// </summary>
public enum ProcessStatus
{
    /// <summary>
    /// ایجاد شده
    /// </summary>
    Created,
    
    
    /// <summary>
    /// در حال شروع
    /// </summary>
    Starting,
    
    /// <summary>
    /// در حال اجرا
    /// </summary>
    Running,
    
    /// <summary>
    /// در حال توقف موقت
    /// </summary>
    Suspending,
    
    /// <summary>
    /// متوقف شده
    /// </summary>
    Suspended,
    
    /// <summary>
    /// در حال ازسرگیری
    /// </summary>
    Resuming,
    
    /// <summary>
    /// در حال تکمیل
    /// </summary>
    Completing,
    
    /// <summary>
    /// تکمیل شده
    /// </summary>
    Completed,
    
    /// <summary>
    /// در حال حذف
    /// </summary>
    Deleting,
    
    /// <summary>
    /// حذف شده
    /// </summary>
    Deleted,
    
    /// <summary>
    /// خطا
    /// </summary>
    Error
}

/// <summary>
/// اطلاعات وظیفه
/// </summary>
public class TaskInfo
{
    /// <summary>
    /// شناسه وظیفه
    /// </summary>
    public string? TaskId { get; set; }
    
    /// <summary>
    /// نوع وظیفه (UserTask, ServiceTask, etc.)
    /// </summary>
    public string? TaskType { get; set; }
    
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
    /// گروه‌های مسئول
    /// </summary>
    public ICollection<string>? CandidateGroups { get; set; }
    
    /// <summary>
    /// کاربران کاندیدا
    /// </summary>
    public ICollection<string>? CandidateUsers { get; set; }
    
    /// <summary>
    /// فرم مرتبط
    /// </summary>
    public string? FormKey { get; set; }
    
    /// <summary>
    /// زمان سررسید
    /// </summary>
    public DateTime? DueDate { get; set; }
    
    /// <summary>
    /// نوع سرویس (فقط برای ServiceTask)
    /// </summary>
    public string? ServiceType { get; set; }
    
    /// <summary>
    /// پارامترهای سرویس (فقط برای ServiceTask)
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }
    
    /// <summary>
    /// داده‌های ارسالی فرم (فقط برای UserTask)
    /// </summary>
    public Dictionary<string, object>? FormData { get; set; }
    
    /// <summary>
    /// متغیرهای فرم (فقط برای UserTask)
    /// </summary>
    public Dictionary<string, object>? FormVariables { get; set; }
    
    /// <summary>
    /// نتیجه اجرا (فقط برای ServiceTask)
    /// </summary>
    public object? Result { get; set; }
    
    /// <summary>
    /// وضعیت وظیفه
    /// </summary>
    public TaskStatus Status { get; set; }
    
    /// <summary>
    /// زمان ایجاد
    /// </summary>
    public DateTime? CreatedAt { get; set; }
    
    /// <summary>
    /// زمان تکمیل
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// وضعیت وظیفه
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// ایجاد شده
    /// </summary>
    Created,
    
    /// <summary>
    /// فعال
    /// </summary>
    Active,
    
    /// <summary>
    /// در حال پردازش
    /// </summary>
    Processing,
    
    /// <summary>
    /// تکمیل شده
    /// </summary>
    Completed,
    
    /// <summary>
    /// لغو شده
    /// </summary>
    Cancelled,
    
    /// <summary>
    /// خطا
    /// </summary>
    Error
}

/// <summary>
/// اطلاعات کار
/// </summary>
public class JobInfo
{
    /// <summary>
    /// شناسه کار
    /// </summary>
    public string? JobId { get; set; }
    
    /// <summary>
    /// شناسه المان مرتبط
    /// </summary>
    public string? ElementId { get; set; }
    
    /// <summary>
    /// نوع المان مرتبط
    /// </summary>
    public string? ElementType { get; set; }
    
    /// <summary>
    /// نوع کار
    /// </summary>
    public string? JobType { get; set; }
    
    /// <summary>
    /// تعداد تلاش‌های باقی‌مانده
    /// </summary>
    public int Retries { get; set; }
    
    /// <summary>
    /// ضرب‌الاجل
    /// </summary>
    public DateTime? Deadline { get; set; }
    
    /// <summary>
    /// شناسه کارگر فعال‌کننده
    /// </summary>
    public string? WorkerId { get; set; }
    
    /// <summary>
    /// هدرهای اختصاصی
    /// </summary>
    public Dictionary<string, string>? CustomHeaders { get; set; }
    
    /// <summary>
    /// نتیجه اجرا
    /// </summary>
    public Dictionary<string, object>? Result { get; set; }
    
    /// <summary>
    /// پیام خطا
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// کد خطا
    /// </summary>
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// زمان اجرای مجدد
    /// </summary>
    public DateTime? RetryBackOff { get; set; }
    
    /// <summary>
    /// وضعیت کار
    /// </summary>
    public JobStatus Status { get; set; }
    
    /// <summary>
    /// زمان ایجاد
    /// </summary>
    public DateTime? CreatedAt { get; set; }
    
    /// <summary>
    /// زمان فعال شدن
    /// </summary>
    public DateTime? ActivatedAt { get; set; }
    
    /// <summary>
    /// زمان تکمیل
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// زمان شکست
    /// </summary>
    public DateTime? FailedAt { get; set; }
    
    /// <summary>
    /// زمان اتمام مهلت
    /// </summary>
    public DateTime? TimedOutAt { get; set; }
    
    /// <summary>
    /// زمان خطا
    /// </summary>
    public DateTime? ErrorAt { get; set; }
}

/// <summary>
/// وضعیت کار
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// ایجاد شده
    /// </summary>
    Created,
    
    /// <summary>
    /// فعال شده
    /// </summary>
    Activated,
    
    /// <summary>
    /// تکمیل شده
    /// </summary>
    Completed,
    
    /// <summary>
    /// شکست خورده
    /// </summary>
    Failed,
    
    /// <summary>
    /// اتمام مهلت
    /// </summary>
    Timeout,
    
    /// <summary>
    /// خطا
    /// </summary>
    Error
}

/// <summary>
/// ورودی تاریخچه رویدادها
/// </summary>
public class HistoryEntry
{
    /// <summary>
    /// شناسه رویداد
    /// </summary>
    public Guid EventId { get; set; }
    
    /// <summary>
    /// نوع رویداد
    /// </summary>
    public string? EventType { get; set; }
    
    /// <summary>
    /// قصد رویداد
    /// </summary>
    public string? Intent { get; set; }
    
    /// <summary>
    /// زمان رویداد
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// شناسه کاربر
    /// </summary>
    public string? UserId { get; set; }
} 