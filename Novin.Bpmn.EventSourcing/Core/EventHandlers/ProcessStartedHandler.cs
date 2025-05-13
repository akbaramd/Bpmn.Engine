using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.Models;
using System.Collections.Generic;
using System.Linq;
using Novin.Bpmn.EventSourcing.Core.Models;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// Handles the creation of new process instances and activates start events
/// </summary>
public class ProcessStartedHandler : BaseEventHandler<ProcessStarted>
{
    private readonly IEventBus _eventBus;

    /// <summary>
    /// Creates a new instance of ProcessCreatedHandler
    /// </summary>
    public ProcessStartedHandler(
        IProcessInstanceStateStore stateStore,
        IEventStore eventStore,
        IProcessDeploymentStore definitionStore,
        IEventBus eventBus,
        ILogger<ProcessStartedHandler> logger)
        : base(stateStore, eventStore, definitionStore, eventBus, logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    /// <inheritdoc />
    protected override async Task ProcessEventAsync(
        ProcessStarted @event, 
        EventHandlerContext context,
        CancellationToken cancellationToken)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        try
        {
            Logger.LogInformation("Handling process creation for instance {ProcessInstanceId}", 
                @event.InstanceId);

            // Get process definition using the process definition ID from the event
            var definition = await GetDefinitionsAsync(@event.DeploymentId, cancellationToken);

            if (definition == null)
            {
                throw new InvalidOperationException($"Process definition {@event.DeploymentKey} not found");
            }

            var defiantionExplorer = GetDefiantionExplorer(definition);

            // Find start events in the definition
            var startEvents = defiantionExplorer.FindStartEvents();

            if (!startEvents.Any())
            {
                throw new InvalidOperationException($"No start events found in process {@event.DeploymentKey}");
            }

            // Create activation events for each start event
            foreach (var startEvent in startEvents)
            {
                Logger.LogDebug("Creating element for start event {StartEventId}", startEvent.id);
                
          
                
                // Create the execution for this start event
                var execution = ElementExecutionBuilder.Init()
                    .WithProcessInstanceId(@event.InstanceId)
                    .WithElementId(startEvent.id)
                    .WithElementType(BpmnElementType.StartEvent)
                    .WithLocalVariables(context.State.Variables)
                    .Executable(true)
                    .Build()
                    .BuildResult();
                
                // Add the execution to the state's concurrent executions dictionary
                context.State.ConcurrentExecutions[execution.ExecutionId] = execution;
                
                Logger.LogDebug("Created execution {ExecutionId} for start event {StartEventId}", 
                    execution.ExecutionId, startEvent.id);
                
                // Create the element created event with this execution ID
                var elementCreatedEvent = new ElementCreated
                {
                    InstanceId = @event.InstanceId,
                    DeploymentKey = @event.DeploymentKey,
                    DeploymentId = @event.DeploymentId,
                    ProcessId = @event.ProcessId,
                    ElementId = startEvent.id,
                    ElementType = BpmnElementType.StartEvent,
                    IsExecutable = true,
                    ExecutionId = execution.ExecutionId,  // Set execution ID explicitly
                    Timestamp = DateTimeOffset.UtcNow
                };
                
                // Add this event to the execution's history
                execution.AddEvent(elementCreatedEvent);
                
                // Publish the event
                PublishLater(elementCreatedEvent);
            }
            
            // Store the state with the new executions
            await StateStore.UpsertAsync(context.State, null, cancellationToken);

            Logger.LogInformation("Process instance {ProcessInstanceId} started successfully with {StartEventCount} start events and executions", 
                @event.InstanceId, startEvents.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling process creation for {ProcessInstanceId}", 
                @event.InstanceId);
            
            // Publish failure event
            PublishLater(new ProcessFailed
            {
                InstanceId = @event.InstanceId,
                DeploymentKey = @event.DeploymentKey,
                DeploymentId = @event.DeploymentId,
                ProcessId = @event.ProcessId,
                ErrorMessage = ex.Message,
                ErrorDetails = ex.StackTrace
            });
            
            throw;
        }
    }
}