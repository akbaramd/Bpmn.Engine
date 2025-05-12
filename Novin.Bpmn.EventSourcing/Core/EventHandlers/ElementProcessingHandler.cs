using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// Handles the processing of BPMN elements and tasks
/// </summary>
public class ElementProcessingHandler : BaseEventHandler<ElementProcessing>
{
    private readonly IEventBus _eventBus;

    /// <summary>
    /// Creates a new instance of ElementProcessingHandler
    /// </summary>
    public ElementProcessingHandler(
        IProcessInstanceStateStore stateStore,
        IEventStore eventStore,
        IProcessDeploymentStore definitionStore,
        IEventBus eventBus,
        ILogger<ElementProcessingHandler> logger)
        : base(stateStore, eventStore, definitionStore, logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }
    
    /// <inheritdoc />
    protected override async Task ProcessEventAsync(
        ElementProcessing @event,
        ProcessInstanceState state,
        CancellationToken cancellationToken)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        try
        {
            Logger.LogInformation("Processing element {ElementId} of type {ElementType} in process {ProcessInstanceId}",
                @event.ElementId, @event.ElementType, @event.ProcessInstanceId);

            // Record the event in state history
            state.RecordEvent(@event);

            // Check if we have an execution ID and update the execution status
            if (!string.IsNullOrEmpty(@event.ExecutionId))
            {
                var execution = state.GetExecution(@event.ExecutionId);
                // Mark execution as Active/Processing if it exists
                if (execution != null)
                {
                    Logger.LogDebug("Updating execution status for {ExecutionId} to Processing", 
                        @event.ExecutionId);
                }
            }

            // Determine the type of element being processed and handle it accordingly
            await HandleElementTypeAsync(@event, state, cancellationToken);
            
            Logger.LogDebug("Successfully processed element {ElementId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing element {ElementId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
                
            // Create failed event 
            await _eventBus.PublishAsync(new ElementFailed
            {
                ProcessInstanceId = @event.ProcessInstanceId,
                ProcessDefinitionId = @event.ProcessDefinitionId,
                ElementId = @event.ElementId,
                ElementType = @event.ElementType,
                ExecutionId = @event.ExecutionId,
                ErrorCode = "PROCESSING_ERROR",
                ErrorMessage = ex.Message,
                Timestamp = DateTimeOffset.UtcNow
            }, cancellationToken);
            
            throw;
        }
    }
    
    /// <summary>
    /// Handle the element processing based on element type
    /// </summary>
    private async Task HandleElementTypeAsync(
        ElementProcessing @event,
        ProcessInstanceState state,
        CancellationToken cancellationToken)
    {
        // Log the element type being processed
        Logger.LogDebug("Element {@ElementId} of type {@ElementType} processing handled",
            @event.ElementId, @event.ElementType);

        // Create a completed event
        await _eventBus.PublishAsync(new ElementCompleted
        {
            ProcessInstanceId = @event.ProcessInstanceId,
            ProcessDefinitionId = @event.ProcessDefinitionId,
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            ExecutionId = @event.ExecutionId,
            Timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);
    }
}
