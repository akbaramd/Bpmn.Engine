using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.EventSourcing.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// پردازش‌کننده رویداد فعال شدن المان
/// </summary>
public class ElementActivatedHandler : IBpmnEventHandler<ElementActivated>
{
    private readonly ILogger<ElementActivatedHandler> _logger;
    private readonly IStateStore _stateStore;
    private readonly IBpmnDefinitionStorage _bpmnDefinitionStorage;
    private readonly IEventBus _eventBus;
    private readonly IUserTaskService _userTaskService;
    
    /// <summary>
    /// ایجاد یک نمونه جدید از پردازش‌کننده رویداد فعال شدن المان
    /// </summary>
    /// <param name="logger">سیستم ثبت وقایع</param>
    /// <param name="stateStore">مخزن وضعیت</param>
    /// <param name="eventBus">گذرگاه رویداد</param>
    public ElementActivatedHandler(
        ILogger<ElementActivatedHandler> logger,
        IStateStore stateStore,
        IEventBus eventBus, 
        IBpmnDefinitionStorage bpmnDefinitionStorage, IUserTaskService userTaskService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _bpmnDefinitionStorage = bpmnDefinitionStorage ?? throw new ArgumentNullException(nameof(bpmnDefinitionStorage));
        _userTaskService = userTaskService;
    }
    
    /// <inheritdoc />
    public async Task HandleAsync(ElementActivated @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Processing ElementActivated event for element {@ElementId} in process {@ProcessInstanceId}", 
            @event.ElementId, @event.ProcessInstanceId);
        
        // ابتدا وضعیت فرآیند را بازیابی می‌کنیم (اگر وجود دارد)
        var (state, version) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
        
        if (state == null)
        {
            // اگر وضعیت وجود نداشت، یک وضعیت جدید ایجاد می‌کنیم
            state = new BpmnProcessState
            {
                ProcessInstanceId = @event.ProcessInstanceId,
                Status = ProcessStatus.Running,
                ActiveElements = new HashSet<string>(),
                CompletedElements = new HashSet<string>(),
                Variables = new Dictionary<string, object>(),
                Tasks = new Dictionary<string, TaskInfo>()
            };
        }
        
        // افزودن المان به لیست المان‌های فعال
        state.ActiveElements.Add(@event.ElementId);
        
        // ذخیره وضعیت آپدیت شده
        await _stateStore.SaveStateAsync(@event.ProcessInstanceId, state, version);
        
        // بررسی نوع المان و انجام عملیات خاص برای هر نوع
       switch (@event.ElementType)
        {
            case "bpmn:UserTask":
                // برای المان‌های UserTask، یک رویداد UserTaskCreated ایجاد می‌کنیم
                await CreateUserTaskAsync(@event);
                break;
                
            case "bpmn:ServiceTask":
                // برای ServiceTask، یک رویداد JobCreated ایجاد می‌کنیم
                await CreateServiceTaskJobAsync(@event);
                break;
                
            case "bpmn:ScriptTask":
                // برای ScriptTask، یک رویداد JobCreated با نوع script-task ایجاد می‌کنیم
                await CreateScriptTaskJobAsync(@event);
                break;
                
            case "bpmn:BusinessRuleTask":
                // برای BusinessRuleTask، یک رویداد JobCreated با نوع business-rule-task ایجاد می‌کنیم
                await CreateBusinessRuleTaskJobAsync(@event);
                break;
                
            case "bpmn:SendTask":
                // برای SendTask، یک رویداد JobCreated با نوع send-task ایجاد می‌کنیم
                await CreateSendTaskJobAsync(@event);
                break;
                
            case "bpmn:ReceiveTask":
                // برای ReceiveTask، یک رویداد SubscriptionCreated ایجاد می‌کنیم
                await CreateReceiveTaskSubscriptionAsync(@event);
                break;
                
            case "bpmn:IntermediateCatchEvent":
                // برای رویدادهای میانی دریافت‌کننده، یک رویداد SubscriptionCreated ایجاد می‌کنیم
                await CreateIntermediateCatchEventSubscriptionAsync(@event);
                break;
                
            case "bpmn:IntermediateThrowEvent":
                // برای رویدادهای میانی ارسال‌کننده، یک رویداد JobCreated ایجاد می‌کنیم
                await CreateIntermediateThrowEventJobAsync(@event);
                break;
                
            case "bpmn:BoundaryEvent":
                // برای رویدادهای مرزی، بسته به نوع آن، یک رویداد مناسب ایجاد می‌کنیم
                await CreateBoundaryEventSubscriptionAsync(@event);
                break;
                
            case "bpmn:CallActivity":
                // برای فعالیت‌های فراخوانی، یک رویداد ProcessInstanceCreating ایجاد می‌کنیم
                await CreateCallActivityAsync(@event);
                break;
                
            case "bpmn:SubProcess":
                // برای زیرفرآیندها، یک رویداد SubProcessStarting ایجاد می‌کنیم
                await CreateSubProcessAsync(@event);
                break;

            case "bpmn:ExclusiveGateway":
            case "bpmn:InclusiveGateway":
            case "bpmn:ParallelGateway":
            case "bpmn:EventBasedGateway":
                // برای گیت‌وی‌ها، یک رویداد GatewayActivated ایجاد می‌کنیم
                await CreateGatewayActivatedAsync(@event);
                break;
                
            default:
                // برای سایر انواع المان‌ها، رویداد خاصی ایجاد نمی‌کنیم و فقط لاگ می‌کنیم
                _logger.LogDebug("Activated element of type {ElementType} with ID {ElementId} in process {ProcessInstanceId}",
                    @event.ElementType, @event.ElementId, @event.ProcessInstanceId);
                break;
        }
    }
    
    /// <summary>
    /// ایجاد یک وظیفه کاربری جدید
    /// </summary>
    private async Task CreateUserTaskAsync(ElementActivated @event)
    {
        try
        {
            // ابتدا وضعیت فرآیند را بازیابی می‌کنیم تا کلید انتشار را بدست آوریم
            var (currentProcessState, _) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
            
            if (currentProcessState == null)
            {
                _logger.LogWarning("Process state not found for {ProcessInstanceId} when creating user task", @event.ProcessInstanceId);
                
                // ایجاد یک وظیفه کاربری ساده با اطلاعات حداقلی
                await _eventBus.PublishAsync(new UserTaskCreatedEvent
                {
                    EventId = Guid.NewGuid(),
                    ProcessInstanceId = @event.ProcessInstanceId,
                    UserTaskId = @event.ElementId,
                    TaskTitle = @event.ElementId,
                    Intent = "CREATED",
                    Timestamp = DateTime.UtcNow
                });
                
                return;
            }
            
            // دریافت XML تعریف فرآیند
            var definition = _bpmnDefinitionStorage.GetParsedDefinition(currentProcessState.DeploymentKey);
            
            if (definition == null)
            {
                _logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", currentProcessState.DeploymentKey);
                
                // ایجاد یک وظیفه کاربری ساده با اطلاعات حداقلی
                await _eventBus.PublishAsync(new UserTaskCreatedEvent
                {
                    EventId = Guid.NewGuid(),
                    ProcessInstanceId = @event.ProcessInstanceId,
                    UserTaskId = @event.ElementId,
                    TaskTitle = @event.ElementId,
                    Intent = "CREATED",
                    Timestamp = DateTime.UtcNow
                });
                
                return;
            }

            // پیدا کردن فرایند و سپس المان UserTask
            var processes = definition.Items.OfType<BpmnProcess>();
            BpmnUserTask userTask = null;
            
            foreach (var process in processes)
            {
                if (process.Items == null) continue;
                
                var foundTask = process.Items
                    .OfType<BpmnUserTask>()
                    .FirstOrDefault(e => e.id == @event.ElementId);
                    
                if (foundTask != null)
                {
                    userTask = foundTask;
                    break;
                }
            }
            
            if (userTask == null)
            {
                _logger.LogWarning("UserTask element with ID {ElementId} not found in definition", @event.ElementId);
                
                // اگر المان پیدا نشد، یک وظیفه کاربری ساده ایجاد می‌کنیم
                await _eventBus.PublishAsync(new UserTaskCreatedEvent
                {
                    EventId = Guid.NewGuid(),
                    ProcessInstanceId = @event.ProcessInstanceId,
                    UserTaskId = @event.ElementId,
                    TaskTitle = @event.ElementId,
                    Intent = "CREATED",
                    Timestamp = DateTime.UtcNow
                });
                
                return;
            }
            
            // استخراج اطلاعات از المان UserTask
            string taskTitle = !string.IsNullOrEmpty(userTask.name) ? userTask.name : @event.ElementId;
            string taskDescription = string.Empty; // Documentation may not be directly available
            
            // استخراج سایر اطلاعات
            string formKey = userTask.formId ?? string.Empty;
            string assignee = userTask.assignee ?? string.Empty;
            int priority = 0; // Default priority
            
            // استخراج کاندیداهای کاربر و گروه
            var candidateUsers = new List<string>();
            if (!string.IsNullOrEmpty(userTask.candidateUsers))
            {
                candidateUsers.AddRange(userTask.candidateUsers.Split(',').Select(u => u.Trim()));
            }
            
            var candidateGroups = new List<string>();
            if (!string.IsNullOrEmpty(userTask.candidateGroups))
            {
                candidateGroups.AddRange(userTask.candidateGroups.Split(',').Select(g => g.Trim()));
            }
            
            // ایجاد وظیفه کاربری با استفاده از سرویس وظایف
            try
            {
                var userTaskInfo = new Core.Models.UserTaskInfo
                {
                    TaskId = @event.ElementId,
                    ProcessInstanceId = @event.ProcessInstanceId,
                    ProcessDefinitionId = currentProcessState?.ProcessDefinitionId,
                    ElementId = @event.ElementId,
                    TaskTitle = taskTitle,
                    TaskDescription = taskDescription,
                    Assignee = assignee,
                    FormKey = formKey,
                    Priority = priority,
                    Status = Core.Models.UserTaskStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                // تنظیم کاندیداهای کاربر و گروه
                if (candidateUsers.Any())
                {
                    userTaskInfo.CandidateUsers = candidateUsers;
                }
                
                if (candidateGroups.Any())
                {
                    userTaskInfo.CandidateGroups = candidateGroups;
                }
                
                // ایجاد وظیفه در سرویس وظایف کاربری
                await _userTaskService.CreateTaskAsync(userTaskInfo);
                _logger.LogDebug("Created user task {TaskId} in UserTaskService", @event.ElementId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user task in UserTaskService for {ElementId}", @event.ElementId);
            }
            
            // ایجاد و ارسال رویداد UserTaskCreated برای پردازش در استریم
            await _eventBus.PublishAsync(new UserTaskCreatedEvent
            {
                EventId = Guid.NewGuid(),
                ProcessInstanceId = @event.ProcessInstanceId,
                UserTaskId = @event.ElementId,
                TaskTitle = taskTitle,
                TaskDescription = taskDescription,
                Assignee = assignee,
                CandidateUsers = candidateUsers.Count > 0 ? candidateUsers : null,
                CandidateGroups = candidateGroups.Count > 0 ? candidateGroups : null,
                FormKey = formKey,
                Priority = priority,
                Intent = "CREATED",
                Timestamp = DateTime.UtcNow
            });
            
            _logger.LogDebug("Created user task {TaskId} with title {TaskTitle} in process {ProcessId}",
                @event.ElementId, taskTitle, @event.ProcessInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user task for {ElementId} in process {ProcessId}",
                @event.ElementId, @event.ProcessInstanceId);
            
            // در صورت بروز خطا، یک وظیفه کاربری ساده ایجاد می‌کنیم
            await _eventBus.PublishAsync(new UserTaskCreatedEvent
            {
                EventId = Guid.NewGuid(),
                ProcessInstanceId = @event.ProcessInstanceId,
                UserTaskId = @event.ElementId,
                TaskTitle = @event.ElementId,
                Intent = "CREATED",
                Timestamp = DateTime.UtcNow
            });
        }
    }
    
    /// <summary>
    /// ایجاد یک کار برای وظیفه سرویس
    /// </summary>
    private async Task CreateServiceTaskJobAsync(ElementActivated @event)
    {
        // اینجا منطق ایجاد کار برای ServiceTask قرار می‌گیرد
        // فعلاً یک رویداد JobCreated پایه ایجاد می‌کنیم
        await _eventBus.PublishAsync(new JobCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            JobId = Guid.NewGuid().ToString(),
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            JobType = "service-task",
            Retries = 3,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// ایجاد اشتراک برای رویداد مرزی
    /// </summary>
    private async Task CreateBoundaryEventSubscriptionAsync(ElementActivated @event)
    {
        try
        {
            // ابتدا وضعیت فرآیند را بازیابی می‌کنیم
            var (state, _) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
            
            if (state == null)
            {
                _logger.LogWarning("Process state not found for {ProcessInstanceId} when creating boundary event subscription", @event.ProcessInstanceId);
                return;
            }

            // بازیابی تعریف BPMN از مخزن
            var definition = _bpmnDefinitionStorage.GetParsedDefinition(state.DeploymentKey);
            if (definition == null)
            {
                _logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", state.DeploymentKey);
                return;
            }

            // پیدا کردن رویداد مرزی در مدل
            var processes = definition.Items.OfType<BpmnProcess>();
            BpmnBoundaryEvent boundaryEvent = null;
            
            foreach (var process in processes)
            {
                if (process.Items == null) continue;
                
                var foundEvent = process.Items
                    .OfType<BpmnBoundaryEvent>()
                    .FirstOrDefault(e => e.id == @event.ElementId);
                    
                if (foundEvent != null)
                {
                    boundaryEvent = foundEvent;
                    break;
                }
            }

            if (boundaryEvent == null)
            {
                _logger.LogWarning("BoundaryEvent element with ID {ElementId} not found in definition", @event.ElementId);
                return;
            }

            // بررسی نوع رویداد (cancelActivity نشان‌دهنده نوع interrupting/non-interrupting است)
            bool isInterrupting = boundaryEvent.cancelActivity;

            // تعیین فعالیتی که رویداد مرزی به آن متصل است
            string attachedTo = boundaryEvent.attachedToRef?.ToString();
            if (string.IsNullOrEmpty(attachedTo))
            {
                _logger.LogWarning("BoundaryEvent {ElementId} does not have attachedToRef attribute", @event.ElementId);
                return;
            }

            // تعیین نوع رویداد مرزی و ایجاد اشتراک مناسب
            if (boundaryEvent.Items == null || boundaryEvent.Items.Length == 0)
            {
                _logger.LogWarning("BoundaryEvent {ElementId} does not have any event definitions", @event.ElementId);
                return;
            }

            // Loop through all event definitions in the boundary event
            foreach (var eventDefinition in boundaryEvent.Items)
            {
                _logger.LogDebug("Processing boundary event definition of type {EventType} for element {ElementId}",
                    eventDefinition.GetType().Name, @event.ElementId);
                
                // بررسی نوع رویداد و ایجاد اشتراک مناسب
                if (eventDefinition is BpmnTimerEventDefinition timerEvent)
                {
                    await CreateTimerEventSubscriptionAsync(@event, isInterrupting, attachedTo, timerEvent);
                }
                else if (eventDefinition is BpmnErrorEventDefinition errorEvent)
                {
                    await CreateErrorEventSubscriptionAsync(@event, isInterrupting, attachedTo, errorEvent);
                }
                else if (eventDefinition is BpmnMessageEventDefinition messageEvent)
                {
                    await CreateMessageEventSubscriptionAsync(@event, isInterrupting, attachedTo, messageEvent);
                }
                else if (eventDefinition is BpmnSignalEventDefinition signalEvent)
                {
                    await CreateSignalEventSubscriptionAsync(@event, isInterrupting, attachedTo, signalEvent);
                }
                else if (eventDefinition is BpmnEscalationEventDefinition escalationEvent)
                {
                    await CreateEscalationEventSubscriptionAsync(@event, isInterrupting, attachedTo, escalationEvent);
                }
                else
                {
                    _logger.LogWarning("Unsupported boundary event type: {EventType} for element {ElementId}", 
                        eventDefinition.GetType().Name, @event.ElementId);
                    
                    // اشتراک عمومی برای انواع پشتیبانی نشده
                    await _eventBus.PublishAsync(new TimerSubscriptionCreatedEvent
                    {
                        EventId = Guid.NewGuid(),
                        ProcessInstanceId = @event.ProcessInstanceId,
                        ElementId = @event.ElementId,
                        TimerId = Guid.NewGuid().ToString(),
                        TimerType = "generic",
                        TimerValue = "PT1H",  // مقدار پیش‌فرض - یک ساعت
                        AttachedToElementId = attachedTo,
                        IsInterrupting = isInterrupting,
                        Intent = "CREATED",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating boundary event subscription for {ElementId} in process {ProcessId}",
                @event.ElementId, @event.ProcessInstanceId);
        }
    }
    
    /// <summary>
    /// ایجاد اشتراک برای رویداد مرزی تایمر
    /// </summary>
    private async Task CreateTimerEventSubscriptionAsync(ElementActivated @event, bool isInterrupting, string attachedTo, BpmnTimerEventDefinition timerEvent)
    {
        try
        {
            // استخراج تنظیمات تایمر
            string timerType = "duration"; // پیش‌فرض
            string timerValue = "PT5M";    // پیش‌فرض: 5 دقیقه
            
            // تعیین نوع تایمر (duration, date, cycle)
            if (timerEvent.TimeDuration != null)
            {
                timerType = "duration";
                timerValue = timerEvent.GetTimeDuration();
            }
            else if (timerEvent.TimeDate != null)
            {
                timerType = "date";
                timerValue = timerEvent.GetTimeDate();
            }
            else if (timerEvent.TimeCycle != null)
            {
                timerType = "cycle";
                timerValue = timerEvent.GetTimeCycle();
            }
            
            // ایجاد اشتراک تایمر
            await _eventBus.PublishAsync(new TimerSubscriptionCreatedEvent
            {
                EventId = Guid.NewGuid(),
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId,
                TimerId = Guid.NewGuid().ToString(),
                TimerType = timerType,
                TimerValue = timerValue,
                AttachedToElementId = attachedTo,
                IsInterrupting = isInterrupting,
                Intent = "CREATED",
                Timestamp = DateTime.UtcNow
            });
            
            _logger.LogDebug("Created timer boundary event subscription for {ElementId} attached to {AttachedTo} in process {ProcessId}. " +
                             "Type: {TimerType}, Value: {TimerValue}, Interrupting: {IsInterrupting}",
                             @event.ElementId, attachedTo, @event.ProcessInstanceId, timerType, timerValue, isInterrupting);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating timer boundary event subscription for {ElementId} in process {ProcessId}",
                @event.ElementId, @event.ProcessInstanceId);
        }
    }
    
    /// <summary>
    /// ایجاد اشتراک برای رویداد مرزی خطا
    /// </summary>
    private async Task CreateErrorEventSubscriptionAsync(ElementActivated @event, bool isInterrupting, string attachedTo, BpmnErrorEventDefinition errorEvent)
    {
        try
        {
            // استخراج اطلاعات خطا
            string errorRef = errorEvent.errorRef?.ToString() ?? "unknown-error";
            string errorCode = errorRef;
            
            // ایجاد اشتراک خطا - برای رویداد خطا از همان TimerSubscriptionCreatedEvent استفاده می‌کنیم
            // در پیاده‌سازی واقعی باید یک کلاس ErrorEventSubscriptionCreatedEvent مجزا ایجاد شود
            await _eventBus.PublishAsync(new TimerSubscriptionCreatedEvent
            {
                EventId = Guid.NewGuid(),
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId,
                TimerId = errorRef,
                TimerType = "error",
                TimerValue = errorCode,
                AttachedToElementId = attachedTo,
                // رویدادهای خطا همیشه interrupting هستند
                IsInterrupting = true,
                Intent = "CREATED",
                Timestamp = DateTime.UtcNow
            });
            
            _logger.LogDebug("Created error boundary event subscription for {ElementId} attached to {AttachedTo} in process {ProcessId}. " +
                             "Error Code: {ErrorCode}",
                             @event.ElementId, attachedTo, @event.ProcessInstanceId, errorCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating error boundary event subscription for {ElementId} in process {ProcessId}",
                @event.ElementId, @event.ProcessInstanceId);
        }
    }
    
    /// <summary>
    /// ایجاد اشتراک برای رویداد مرزی پیام
    /// </summary>
    private async Task CreateMessageEventSubscriptionAsync(ElementActivated @event, bool isInterrupting, string attachedTo, BpmnMessageEventDefinition messageEvent)
    {
        try
        {
            // استخراج اطلاعات پیام
            string messageRef = messageEvent.messageRef?.ToString() ?? "unknown-message";
            string messageName = messageRef;
            
            // ایجاد اشتراک پیام - استفاده از MessageSubscriptionCreatedEvent
            await _eventBus.PublishAsync(new MessageSubscriptionCreatedEvent
            {
                EventId = Guid.NewGuid(),
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId,
                MessageName = messageName,
                AttachedToElementId = attachedTo,
                IsInterrupting = isInterrupting,
                Intent = "CREATED",
                Timestamp = DateTime.UtcNow
            });
            
            _logger.LogDebug("Created message boundary event subscription for {ElementId} attached to {AttachedTo} in process {ProcessId}. " +
                             "Message: {MessageName}, Interrupting: {IsInterrupting}",
                             @event.ElementId, attachedTo, @event.ProcessInstanceId, messageName, isInterrupting);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating message boundary event subscription for {ElementId} in process {ProcessId}",
                @event.ElementId, @event.ProcessInstanceId);
        }
    }
    
    /// <summary>
    /// ایجاد اشتراک برای رویداد مرزی سیگنال
    /// </summary>
    private async Task CreateSignalEventSubscriptionAsync(ElementActivated @event, bool isInterrupting, string attachedTo, BpmnSignalEventDefinition signalEvent)
    {
        try
        {
            // استخراج اطلاعات سیگنال
            string signalRef = signalEvent.signalRef?.ToString() ?? "unknown-signal";
            string signalName = signalRef;
            
            // ایجاد اشتراک سیگنال
            await _eventBus.PublishAsync(new MessageSubscriptionCreatedEvent
            {
                EventId = Guid.NewGuid(),
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId,
                MessageName = "signal:" + signalName,
                AttachedToElementId = attachedTo,
                IsInterrupting = isInterrupting,
                Intent = "CREATED",
                Timestamp = DateTime.UtcNow
            });
            
            _logger.LogDebug("Created signal boundary event subscription for {ElementId} attached to {AttachedTo} in process {ProcessId}. " +
                             "Signal: {SignalName}, Interrupting: {IsInterrupting}",
                             @event.ElementId, attachedTo, @event.ProcessInstanceId, signalName, isInterrupting);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating signal boundary event subscription for {ElementId} in process {ProcessId}",
                @event.ElementId, @event.ProcessInstanceId);
        }
    }
    
    /// <summary>
    /// ایجاد اشتراک برای رویداد مرزی اسکالیشن
    /// </summary>
    private async Task CreateEscalationEventSubscriptionAsync(ElementActivated @event, bool isInterrupting, string attachedTo, BpmnEscalationEventDefinition escalationEvent)
    {
        try
        {
            // استخراج اطلاعات escalation
            string escalationRef = escalationEvent.escalationRef?.ToString() ?? "unknown-escalation";
            string escalationCode = escalationRef;
            
            // ایجاد اشتراک اسکالیشن
            await _eventBus.PublishAsync(new MessageSubscriptionCreatedEvent
            {
                EventId = Guid.NewGuid(),
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId,
                MessageName = "escalation:" + escalationCode,
                AttachedToElementId = attachedTo,
                IsInterrupting = isInterrupting,
                Intent = "CREATED",
                Timestamp = DateTime.UtcNow
            });
            
            _logger.LogDebug("Created escalation boundary event subscription for {ElementId} attached to {AttachedTo} in process {ProcessId}. " +
                             "Escalation Code: {EscalationCode}, Interrupting: {IsInterrupting}",
                             @event.ElementId, attachedTo, @event.ProcessInstanceId, escalationCode, isInterrupting);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating escalation boundary event subscription for {ElementId} in process {ProcessId}",
                @event.ElementId, @event.ProcessInstanceId);
        }
    }
    
    /// <summary>
    /// ایجاد کار برای ScriptTask
    /// </summary>
    private async Task CreateScriptTaskJobAsync(ElementActivated @event)
    {
        // برای فعلا یک کار ساده ایجاد می‌کنیم
        await _eventBus.PublishAsync(new JobCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            JobId = Guid.NewGuid().ToString(),
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            JobType = "script-task",
            Retries = 3,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow
        });
    }
    
    /// <summary>
    /// ایجاد کار برای BusinessRuleTask
    /// </summary>
    private async Task CreateBusinessRuleTaskJobAsync(ElementActivated @event)
    {
        await _eventBus.PublishAsync(new JobCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            JobId = Guid.NewGuid().ToString(),
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            JobType = "business-rule-task",
            Retries = 3,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow
        });
    }
    
    /// <summary>
    /// ایجاد کار برای SendTask
    /// </summary>
    private async Task CreateSendTaskJobAsync(ElementActivated @event)
    {
        await _eventBus.PublishAsync(new JobCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            JobId = Guid.NewGuid().ToString(),
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            JobType = "send-task",
            Retries = 3,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow
        });
    }
    
    /// <summary>
    /// ایجاد اشتراک برای IntermediateCatchEvent
    /// </summary>
    private async Task CreateIntermediateCatchEventSubscriptionAsync(ElementActivated @event)
    {
        // برای فعلا یک اشتراک پیام ساده ایجاد می‌کنیم
        await _eventBus.PublishAsync(new MessageSubscriptionCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            ElementId = @event.ElementId,
            MessageName = @event.ElementId,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow
        });
    }
    
    /// <summary>
    /// ایجاد کار برای IntermediateThrowEvent
    /// </summary>
    private async Task CreateIntermediateThrowEventJobAsync(ElementActivated @event)
    {
        // برای فعلا یک کار پایه ایجاد می‌کنیم
        await _eventBus.PublishAsync(new JobCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            JobId = Guid.NewGuid().ToString(),
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            JobType = "throw-event",
            Retries = 3,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow
        });
    }
    
    /// <summary>
    /// ایجاد فرآیند برای CallActivity
    /// </summary>
    private async Task CreateCallActivityAsync(ElementActivated @event)
    {
        await _eventBus.PublishAsync(new JobCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            JobId = Guid.NewGuid().ToString(),
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            JobType = "call-activity",
            Retries = 3,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow
        });
    }
    
    /// <summary>
    /// ایجاد زیرفرآیند
    /// </summary>
    private async Task CreateSubProcessAsync(ElementActivated @event)
    {
        // برای فعلا یک رویداد آغاز زیرفرآیند ساده ایجاد می‌کنیم
        await _eventBus.PublishAsync(new SubProcessStartingEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            SubProcessId = @event.ElementId,
            Timestamp = DateTime.UtcNow
        });
    }
    
    /// <summary>
    /// ایجاد رویداد فعال‌سازی گیت‌وی
    /// </summary>
    private async Task CreateGatewayActivatedAsync(ElementActivated @event)
    {
        // برای گیت‌وی‌ها می‌توانیم رویداد فعال‌سازی ایجاد کنیم
        await _eventBus.PublishAsync(new GatewayActivatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            GatewayId = @event.ElementId,
            GatewayType = @event.ElementType,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// ایجاد اشتراک برای ReceiveTask
    /// </summary>
    private async Task CreateReceiveTaskSubscriptionAsync(ElementActivated @event)
    {
        try
        {
            // ساده‌سازی شده: یک اشتراک پیام ساده ایجاد می‌کنیم
            await _eventBus.PublishAsync(new MessageSubscriptionCreatedEvent
            {
                EventId = Guid.NewGuid(),
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId,
                MessageName = @event.ElementId,
                Intent = "CREATED",
                Timestamp = DateTime.UtcNow
            });
            
            _logger.LogDebug("Created simplified message subscription for receive task {ElementId} in process {ProcessId}",
                @event.ElementId, @event.ProcessInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating message subscription for receive task {ElementId} in process {ProcessId}",
                @event.ElementId, @event.ProcessInstanceId);
            
            // در صورت خطا، یک اشتراک پیام ساده ایجاد می‌کنیم
            await _eventBus.PublishAsync(new MessageSubscriptionCreatedEvent
            {
                EventId = Guid.NewGuid(),
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId,
                MessageName = @event.ElementId,
                Intent = "CREATED",
                Timestamp = DateTime.UtcNow
            });
        }
    }
}

/// <summary>
/// رویداد ایجاد کار جدید
/// </summary>
