using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// Handles the completion of BPMN elements in a process instance
/// </summary>
public class ElementCompletedHandler : BaseEventHandler<ElementCompleted>
{
    private readonly IEventBus _eventBus;

    /// <summary>
    /// Creates a new instance of ElementCompletedHandler
    /// </summary>
    public ElementCompletedHandler(
        IProcessInstanceStateStore stateStore,
        IEventStore eventStore,
        IProcessDeploymentStore definitionStore, 
        IEventBus eventBus,
        ILogger<ElementCompletedHandler> logger)
        : base(stateStore, eventStore, definitionStore, logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    /// <inheritdoc />
    protected override async Task ProcessEventAsync(
        ElementCompleted @event, 
        ProcessInstanceState state, 
        CancellationToken cancellationToken)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        try
        {
            Logger.LogDebug("Processing ElementCompleted event for element {ElementId} in process {ProcessInstanceId}", 
                @event.ElementId, @event.ProcessInstanceId);

            // Record the event in state history
            state.RecordEvent(@event);

            // If we have an execution ID, mark that execution as completed
            if (!string.IsNullOrEmpty(@event.ExecutionId))
            {
                try 
                {
                    Logger.LogDebug("Marked execution {ExecutionId} as completed for element {ElementId}", 
                        @event.ExecutionId, @event.ElementId);
                } 
                catch (KeyNotFoundException)
                {
                    Logger.LogWarning("Could not find execution {ExecutionId} for element {ElementId}",
                        @event.ExecutionId, @event.ElementId);
                }
            }
            
            // Handle special element types
            if (@event.ElementType == BpmnElementType.EndEvent)
            {
                await HandleEndEventAsync(state, @event, cancellationToken);
            }
            else if (@event.ElementType == BpmnElementType.Task || 
                     @event.ElementType == BpmnElementType.UserTask ||
                     @event.ElementType == BpmnElementType.ServiceTask ||
                     @event.ElementType == BpmnElementType.ScriptTask ||
                     @event.ElementType == BpmnElementType.BusinessRuleTask ||
                     @event.ElementType == BpmnElementType.SendTask ||
                     @event.ElementType == BpmnElementType.ReceiveTask)
            {
                await HandleTaskCompletionAsync(state, @event, cancellationToken);
            }
            else if (@event.ElementType == BpmnElementType.ParallelGateway ||
                     @event.ElementType == BpmnElementType.ExclusiveGateway ||
                     @event.ElementType == BpmnElementType.InclusiveGateway)
            {
                await HandleGatewayCompletionAsync(state, @event, cancellationToken);
            }
            else
            {
                Logger.LogInformation("Element {ElementId} of type {ElementType} completed successfully in process {ProcessInstanceId}",
                    @event.ElementId, @event.ElementType, @event.ProcessInstanceId);
                await ScheduleOutgoingFlowsAsync(state, @event, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling ElementCompleted event for element {ElementId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            throw;
        }
    }

    private async Task HandleEndEventAsync(
        ProcessInstanceState state, 
        ElementCompleted @event, 
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Processing completion of end event {ElementId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        // Publish a process completed event
        await _eventBus.PublishAsync(new ProcessCompleted
        {
            ProcessInstanceId = @event.ProcessInstanceId,
            CompletedAt = DateTime.UtcNow
        }, cancellationToken);
        
        Logger.LogInformation("Process {ProcessInstanceId} completed with end event {ElementId}",
            @event.ProcessInstanceId, @event.ElementId);
    }
    
    private async Task HandleTaskCompletionAsync(
        ProcessInstanceState state, 
        ElementCompleted @event, 
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Processing completion of task {ElementId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        // Schedule processing of outgoing flows
        await ScheduleOutgoingFlowsAsync(state, @event, cancellationToken);
    }
    
    private async Task HandleGatewayCompletionAsync(
        ProcessInstanceState state, 
        ElementCompleted @event, 
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Processing completion of gateway {ElementId} of type {ElementType} in process {ProcessInstanceId}",
            @event.ElementId, @event.ElementType, @event.ProcessInstanceId);
            
        // This would handle gateway-specific logic based on gateway type
        // For now, we'll just schedule outgoing flows
        await ScheduleOutgoingFlowsAsync(state, @event, cancellationToken);
    }
    
    private async Task ScheduleOutgoingFlowsAsync(
        ProcessInstanceState state, 
        ElementCompleted @event, 
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Scheduling processing of outgoing flows for element {ElementId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        // This would typically:
        // 1. Query the BPMN definition for outgoing sequence flows
        // 2. Apply gateway-specific logic (exclusive, inclusive, parallel)
        // 3. Create activation events for next elements
        
        // For now, just log the action
        Logger.LogInformation("Outgoing flows scheduled for processing from element {ElementId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
    }
} 