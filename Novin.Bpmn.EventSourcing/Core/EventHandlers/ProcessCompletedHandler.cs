using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// Handles the completion of process instances and performs cleanup
/// </summary>
public class ProcessCompletedHandler : BaseEventHandler<ProcessCompleted>
{
    private readonly IEventBus _eventBus;

    /// <summary>
    /// Creates a new instance of ProcessCompletedHandler
    /// </summary>
    public ProcessCompletedHandler(
        IProcessInstanceStateStore stateStore,
        IEventStore eventStore,
        IProcessDeploymentStore definitionStore,
        IEventBus eventBus,
        ILogger<ProcessCompletedHandler> logger)
        : base(stateStore, eventStore, definitionStore, eventBus, logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }
    
    /// <inheritdoc />
    protected override async Task ProcessEventAsync(
        ProcessCompleted @event, 
        EventHandlerContext context,
        CancellationToken cancellationToken)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }
        
        try
        {
            Logger.LogDebug("Processing ProcessCompleted event for process instance {ProcessInstanceId}", 
                @event.InstanceId);
            

            // Update process state to completed
            context.State.Complete(@event);
            
            Logger.LogInformation("Successfully completed process instance {ProcessInstanceId}",
                @event.InstanceId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling ProcessCompleted event for process instance {ProcessInstanceId}",
                @event.InstanceId);
            throw;
        }
    }
} 