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
public class ProcessCreatedHandler : BaseEventHandler<ProcessStarted>
{
    private readonly IEventBus _eventBus;

    /// <summary>
    /// Creates a new instance of ProcessCreatedHandler
    /// </summary>
    public ProcessCreatedHandler(
        IProcessInstanceStateStore stateStore,
        IEventStore eventStore,
        IProcessDeploymentStore definitionStore,
        IEventBus eventBus,
        ILogger<ProcessCreatedHandler> logger)
        : base(stateStore, eventStore, definitionStore, logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    /// <inheritdoc />
    protected override async Task ProcessEventAsync(
        ProcessStarted @event, 
        ProcessInstanceState state,
        CancellationToken cancellationToken)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        try
        {
            Logger.LogInformation("Handling process creation for instance {ProcessInstanceId}", 
                @event.ProcessInstanceId);

            // Record the event in state history
            state.RecordEvent(@event);

            // Get process definition using the process definition ID from the event
            var definition = await DefinitionStore.GetDefinitionsAsync(
                @event.ProcessDefinitionId, 
                cancellationToken);

            if (definition == null)
            {
                throw new InvalidOperationException($"Process definition {@event.ProcessDefinitionId} not found");
            }

            // Find start events in the definition
            var startEvents = FindStartEvents(definition);
            if (!startEvents.Any())
            {
                throw new InvalidOperationException($"No start events found in process {@event.ProcessDefinitionId}");
            }

            // Create activation events for each start event
            foreach (var startEvent in startEvents)
            {
                Logger.LogDebug("Creating element for start event {StartEventId}", startEvent.id);
                
                await _eventBus.PublishAsync(new ElementCreated
                {
                    ProcessInstanceId = @event.ProcessInstanceId,
                    ProcessDefinitionId = @event.ProcessDefinitionId,
                    ElementId = startEvent.id,
                    ElementType = BpmnElementType.StartEvent,
                    IsExecutable = true,
                    Timestamp = DateTimeOffset.UtcNow
                }, cancellationToken);
            }

            Logger.LogInformation("Process instance {ProcessInstanceId} started successfully with {StartEventCount} start events", 
                @event.ProcessInstanceId, startEvents.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling process creation for {ProcessInstanceId}", 
                @event.ProcessInstanceId);
            
            // Publish failure event
            await _eventBus.PublishAsync(new ProcessFailed
            {
                ProcessInstanceId = @event.ProcessInstanceId,
                ErrorMessage = ex.Message,
                ErrorDetails = ex.StackTrace
            }, cancellationToken);
            
            throw;
        }
    }

    private List<BpmnStartEvent> FindStartEvents(BpmnDefinitions definition)
    {
        var startEvents = new List<BpmnStartEvent>();
        
        if (definition?.Items == null)
            return startEvents;
            
        // First find all processes
        var processes = definition.Items.OfType<BpmnProcess>().ToList();
        
        foreach (var process in processes)
        {
            if (process.Items == null)
                continue;
                
            startEvents.AddRange(process.Items.OfType<BpmnStartEvent>());
        }
        
        return startEvents;
    }
}