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
/// Handles the processing of BPMN elements and tasks
/// </summary>
public class ElementProcessingHandler : IBpmnEventHandler<ElementProcessing>
{
    private readonly ILogger<ElementProcessingHandler> _logger;
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
    public ElementProcessingHandler(
        ILogger<ElementProcessingHandler> logger,
        IStateStore stateStore,
        IEventBus eventBus, 
        IBpmnDefinitionStorage bpmnDefinitionStorage, 
        IUserTaskService userTaskService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _bpmnDefinitionStorage = bpmnDefinitionStorage ?? throw new ArgumentNullException(nameof(bpmnDefinitionStorage));
        _userTaskService = userTaskService;
    }
    
    /// <inheritdoc />
    public async Task HandleAsync(ElementProcessing @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Processing element {ElementId} of type {ElementType} in process {ProcessInstanceId}", 
            @event.ElementId, @event.ElementType, @event.ProcessInstanceId);
        
        var (state, version) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
        
        if (state == null)
        {
            _logger.LogWarning("Process state not found for {ProcessInstanceId}", @event.ProcessInstanceId);
            return;
        }

        try
        {
            switch (@event.ElementType)
        {
            case "bpmn:UserTask":
                    await ProcessUserTaskAsync(@event, state, version, cancellationToken);
                break;
                
            case "bpmn:ServiceTask":
                    await ProcessServiceTaskAsync(@event, state, version, cancellationToken);
                break;
                
            case "bpmn:ScriptTask":
                    await ProcessScriptTaskAsync(@event, state, version, cancellationToken);
                break;
                
            case "bpmn:BusinessRuleTask":
                    await ProcessBusinessRuleTaskAsync(@event, state, version, cancellationToken);
                break;
                
            case "bpmn:SendTask":
                    await ProcessSendTaskAsync(@event, state, version, cancellationToken);
                break;
                
            case "bpmn:ReceiveTask":
                    await ProcessReceiveTaskAsync(@event, state, version, cancellationToken);
                break;
                
            case "bpmn:IntermediateCatchEvent":
                    await ProcessIntermediateCatchEventAsync(@event, state, version, cancellationToken);
                break;
                
            case "bpmn:IntermediateThrowEvent":
                    await ProcessIntermediateThrowEventAsync(@event, state, version, cancellationToken);
                break;
                
            case "bpmn:BoundaryEvent":
                    await ProcessBoundaryEventAsync(@event, state, version, cancellationToken);
                break;
                
            case "bpmn:CallActivity":
                    await ProcessCallActivityAsync(@event, state, version, cancellationToken);
                break;
                
            case "bpmn:SubProcess":
                    await ProcessSubProcessAsync(@event, state, version, cancellationToken);
                break;

                default:
                    _logger.LogDebug("Processing element {ElementId} of type {ElementType}", 
                        @event.ElementId, @event.ElementType);
                    await CompleteElementAsync(@event, state, version, cancellationToken);
                break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing element {ElementId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
                
            await _eventBus.PublishAsync(new ElementFailed
            {
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId,
                ElementType = @event.ElementType,
                ErrorCode = "PROCESSING_ERROR",
                ErrorMessage = ex.Message
            }, cancellationToken);
        }
    }
    
    private async Task ProcessUserTaskAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        try
        {
            var definition = _bpmnDefinitionStorage.GetParsedDefinition(state.DeploymentKey);
            if (definition == null)
            {
                throw new InvalidOperationException($"BPMN definition not found for deployment key {state.DeploymentKey}");
            }

            var userTask = FindUserTask(definition, @event.ElementId);
            if (userTask == null)
            {
                throw new InvalidOperationException($"UserTask {@event.ElementId} not found in definition");
            }

            var taskInfo = new UserTaskInfo
            {
                TaskId = @event.ElementId,
                ProcessInstanceId = @event.ProcessInstanceId,
                ProcessDefinitionId = state.ProcessDefinitionId,
                ElementId = @event.ElementId,
                TaskTitle = userTask.name ?? @event.ElementId,
                TaskDescription = string.Empty,
                Assignee = userTask.assignee,
                FormKey = userTask.formId,
                Priority = 0,
                Status = UserTaskStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (!string.IsNullOrEmpty(userTask.candidateUsers))
            {
                taskInfo.CandidateUsers = userTask.candidateUsers.Split(',').Select(u => u.Trim()).ToList();
            }
            
            if (!string.IsNullOrEmpty(userTask.candidateGroups))
            {
                taskInfo.CandidateGroups = userTask.candidateGroups.Split(',').Select(g => g.Trim()).ToList();
            }

            await _userTaskService.CreateTaskAsync(taskInfo);
            
            _logger.LogDebug("Created user task {TaskId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing user task {TaskId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            throw;
        }
    }
    
    private async Task ProcessServiceTaskAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
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
        }, cancellationToken);
    }
    
    private async Task ProcessScriptTaskAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
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
        }, cancellationToken);
    }
    
    private async Task ProcessBusinessRuleTaskAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
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
        }, cancellationToken);
    }
    
    private async Task ProcessSendTaskAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
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
        }, cancellationToken);
    }
    
    private async Task ProcessReceiveTaskAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        await _eventBus.PublishAsync(new MessageSubscriptionCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            ElementId = @event.ElementId,
            MessageName = @event.ElementId,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow
        }, cancellationToken);
    }
    
    private async Task ProcessIntermediateCatchEventAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        await _eventBus.PublishAsync(new MessageSubscriptionCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            ElementId = @event.ElementId,
            MessageName = @event.ElementId,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow
        }, cancellationToken);
    }
    
    private async Task ProcessIntermediateThrowEventAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
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
        }, cancellationToken);
    }
    
    private async Task ProcessBoundaryEventAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        var definition = _bpmnDefinitionStorage.GetParsedDefinition(state.DeploymentKey);
        if (definition == null)
        {
            throw new InvalidOperationException($"BPMN definition not found for deployment key {state.DeploymentKey}");
        }

        var boundaryEvent = FindBoundaryEvent(definition, @event.ElementId);
        if (boundaryEvent == null)
        {
            throw new InvalidOperationException($"BoundaryEvent {@event.ElementId} not found in definition");
        }

        foreach (var eventDefinition in boundaryEvent.Items ?? Array.Empty<object>())
        {
            if (eventDefinition is BpmnTimerEventDefinition timerEvent)
            {
                await ProcessTimerEventAsync(@event, timerEvent, boundaryEvent, cancellationToken);
            }
            else if (eventDefinition is BpmnMessageEventDefinition messageEvent)
            {
                await ProcessMessageEventAsync(@event, messageEvent, boundaryEvent, cancellationToken);
            }
            else if (eventDefinition is BpmnErrorEventDefinition errorEvent)
            {
                await ProcessErrorEventAsync(@event, errorEvent, boundaryEvent, cancellationToken);
            }
            else if (eventDefinition is BpmnSignalEventDefinition signalEvent)
            {
                await ProcessSignalEventAsync(@event, signalEvent, boundaryEvent, cancellationToken);
            }
        }
    }
    
    private async Task ProcessCallActivityAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
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
        }, cancellationToken);
    }
    
    private async Task ProcessSubProcessAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        await _eventBus.PublishAsync(new SubProcessStartingEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            SubProcessId = @event.ElementId,
            Timestamp = DateTime.UtcNow
        }, cancellationToken);
    }
    
    private async Task CompleteElementAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        await _eventBus.PublishAsync(new ElementCompleted
        {
            ProcessInstanceId = @event.ProcessInstanceId,
            ElementId = @event.ElementId,
            ElementType = @event.ElementType
        }, cancellationToken);
    }
    
    private BpmnUserTask FindUserTask(BpmnDefinitions definitions, string taskId)
    {
        foreach (var process in definitions.Items.OfType<BpmnProcess>())
        {
            var task = process.Items?.OfType<BpmnUserTask>()
                .FirstOrDefault(t => t.id == taskId);
                
            if (task != null)
                return task;
        }
        
        return null;
    }
    
    private BpmnBoundaryEvent FindBoundaryEvent(BpmnDefinitions definitions, string eventId)
    {
        foreach (var process in definitions.Items.OfType<BpmnProcess>())
        {
            var boundaryEvent = process.Items?.OfType<BpmnBoundaryEvent>()
                .FirstOrDefault(e => e.id == eventId);
                
            if (boundaryEvent != null)
                return boundaryEvent;
        }
        
        return null;
    }
    
    private async Task ProcessTimerEventAsync(
        ElementProcessing @event,
        BpmnTimerEventDefinition timerEvent,
        BpmnBoundaryEvent boundaryEvent,
        CancellationToken cancellationToken)
    {
        string timerType = "duration";
        string timerValue = "PT5M";
        
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
        
        await _eventBus.PublishAsync(new TimerSubscriptionCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            ElementId = @event.ElementId,
            TimerId = Guid.NewGuid().ToString(),
            TimerType = timerType,
            TimerValue = timerValue,
            AttachedToElementId = boundaryEvent.attachedToRef?.Name,
            IsInterrupting = boundaryEvent.cancelActivity,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow
        }, cancellationToken);
    }
    
    private async Task ProcessMessageEventAsync(
        ElementProcessing @event,
        BpmnMessageEventDefinition messageEvent,
        BpmnBoundaryEvent boundaryEvent,
        CancellationToken cancellationToken)
    {
        string messageRef = messageEvent.messageRef?.Name ?? "unknown-message";
        
        await _eventBus.PublishAsync(new MessageSubscriptionCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            ElementId = @event.ElementId,
            MessageName = messageRef,
            AttachedToElementId = boundaryEvent.attachedToRef?.Name,
            IsInterrupting = boundaryEvent.cancelActivity,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow
        }, cancellationToken);
    }
    
    private async Task ProcessErrorEventAsync(
        ElementProcessing @event,
        BpmnErrorEventDefinition errorEvent,
        BpmnBoundaryEvent boundaryEvent,
        CancellationToken cancellationToken)
    {
        string errorRef = errorEvent.errorRef?.Name ?? "unknown-error";
        
        await _eventBus.PublishAsync(new TimerSubscriptionCreatedEvent
            {
                EventId = Guid.NewGuid(),
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId,
            TimerId = errorRef,
            TimerType = "error",
            TimerValue = errorRef,
            AttachedToElementId = boundaryEvent.attachedToRef?.Name,
            IsInterrupting = true,
                Intent = "CREATED",
                Timestamp = DateTime.UtcNow
        }, cancellationToken);
    }
    
    private async Task ProcessSignalEventAsync(
        ElementProcessing @event,
        BpmnSignalEventDefinition signalEvent,
        BpmnBoundaryEvent boundaryEvent,
        CancellationToken cancellationToken)
    {
        string signalRef = signalEvent.signalRef?.Name ?? "unknown-signal";
        
            await _eventBus.PublishAsync(new MessageSubscriptionCreatedEvent
            {
                EventId = Guid.NewGuid(),
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId,
            MessageName = "signal:" + signalRef,
            AttachedToElementId = boundaryEvent.attachedToRef?.Name,
            IsInterrupting = boundaryEvent.cancelActivity,
                Intent = "CREATED",
                Timestamp = DateTime.UtcNow
        }, cancellationToken);
    }
}

/// <summary>
/// رویداد ایجاد کار جدید
/// </summary>
