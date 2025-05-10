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
    /// وضعیت المان‌ها
    /// </summary>
    public Dictionary<string, ElementStatus> ElementStatuses { get; set; } = new();
    
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