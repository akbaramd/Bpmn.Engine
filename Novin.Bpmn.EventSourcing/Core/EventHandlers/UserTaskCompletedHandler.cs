using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// هندلر رویداد تکمیل وظیفه کاربری
/// </summary>
public class UserTaskCompletedHandler : IBpmnEventHandler<UserTaskCompletedEvent>
{
    private readonly ILogger<UserTaskCompletedHandler> _logger;
    private readonly IEventBus _eventBus;
    private readonly IStateStore _stateStore;
    
    /// <summary>
    /// ایجاد یک نمونه جدید از هندلر رویداد تکمیل وظیفه کاربری
    /// </summary>
    public UserTaskCompletedHandler(
        ILogger<UserTaskCompletedHandler> logger,
        IEventBus eventBus,
        IStateStore stateStore)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }
    
    /// <inheritdoc />
    public async Task HandleAsync(Events.UserTaskCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Handling UserTaskCompletedEvent for process {ProcessInstanceId}, task {UserTaskId}",
            @event.ProcessInstanceId, @event.UserTaskId);
        
        try
        {
            // اول تکمیل المان را شروع می‌کنیم
            await _eventBus.PublishAsync(new ElementCompleting
            {
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.UserTaskId,
                ElementType = "bpmn:UserTask",
                Timestamp = @event.Timestamp
            }, cancellationToken);
            
            // کمی صبر می‌کنیم تا پردازش شود
            await Task.Delay(50, cancellationToken);
            
            // سپس تکمیل المان را تائید می‌کنیم تا جریان فرآیند ادامه یابد
            await _eventBus.PublishAsync(new ElementCompleted
            {
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.UserTaskId,
                ElementType = "bpmn:UserTask",
                Timestamp = @event.Timestamp
            }, cancellationToken);
            
            _logger.LogInformation("Published ElementCompleted event for user task {UserTaskId} in process {ProcessInstanceId}",
                @event.UserTaskId, @event.ProcessInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling UserTaskCompletedEvent for process {ProcessInstanceId}, task {UserTaskId}",
                @event.ProcessInstanceId, @event.UserTaskId);
            throw;
        }
    }
} 