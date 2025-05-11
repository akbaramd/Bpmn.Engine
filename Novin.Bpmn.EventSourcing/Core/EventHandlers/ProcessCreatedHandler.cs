using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// Handles the creation of new process instances and activates start events
/// </summary>
public class ProcessCreatedHandler : IBpmnEventHandler<ProcessInstanceCreated>
{
    private readonly ILogger<ProcessCreatedHandler> _logger;
    private readonly IStateStore _stateStore;
    private readonly IEventBus _eventBus;
    private readonly IBpmnDefinitionStorage _definitionStorage;
    
    public ProcessCreatedHandler(
        ILogger<ProcessCreatedHandler> logger,
        IStateStore stateStore,
        IEventBus eventBus,
        IBpmnDefinitionStorage definitionStorage)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _definitionStorage = definitionStorage ?? throw new ArgumentNullException(nameof(definitionStorage));
    }
    
    public async Task HandleAsync(ProcessInstanceCreated @event, CancellationToken cancellationToken = default)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }
        
        try
        {
            _logger.LogDebug("Processing ProcessInstanceCreated event for process instance {ProcessInstanceId}", 
                @event.ProcessInstanceId);
            
            // Get BPMN definition
            var definitions = _definitionStorage.GetParsedDefinition(@event.ProcessDefinitionKey);
            if (definitions == null)
            {
                throw new InvalidOperationException($"BPMN definition not found for deployment key {@event.ProcessDefinitionKey}");
            }
            
            // Find process definition
            var process = FindProcess(definitions, @event.ProcessDefinitionId);
            if (process == null)
            {
                throw new InvalidOperationException($"Process definition {@event.ProcessDefinitionId} not found");
            }
            
            // Find all start events
            var startEvents = FindStartEvents(process);
            if (!startEvents.Any())
            {
                throw new InvalidOperationException($"No start events found in process {process.id}");
            }
            
            // Get current state
            var state = await _stateStore.GetStateAsync<BpmnProcessState>(@event.ProcessInstanceId);
            if (state == null)
            {
                throw new InvalidOperationException($"Process instance state not found for {@event.ProcessInstanceId}");
            }
            
            // Create an initial root execution path for the process instance
            var rootExecutionPath = new Core.Models.ExecutionPath
            {
                ExecutionId = Guid.NewGuid().ToString(),
                SourceElementId = "process_start",
                SourceElementType = "process",
                TargetElementId = startEvents.First().id, // Use the first start event as target
                TargetElementType = "bpmn:StartEvent",
                Status = ExecutionStatus.Active
            };
            
            // Add the execution path to state
            state.ExecutionPaths.Add(rootExecutionPath);
            state.ActiveExecutions[rootExecutionPath.ExecutionId] = rootExecutionPath;
            
            // Add the event to the execution path
            rootExecutionPath.AddEvent(@event);
            state.EventToExecutionPath[@event.EventId.ToString()] = rootExecutionPath.ExecutionId;
            
            // Save updated state
            await _stateStore.SaveStateAsync(@event.ProcessInstanceId, state, 1);
            
            // Activate each start event
            foreach (var startEvent in startEvents)
            {
                _logger.LogDebug("Activating start event {StartEventId} in process {ProcessInstanceId}",
                    startEvent.id, @event.ProcessInstanceId);
                
                // Add to active elements
                state.ActiveElements.Add(startEvent.id);
                
                // Create a child execution path for this start event
                var startEventExecution = new Core.Models.ExecutionPath
                {
                    ExecutionId = Guid.NewGuid().ToString(),
                    SourceElementId = "process_start",
                    SourceElementType = "process",
                    TargetElementId = startEvent.id,
                    TargetElementType = "bpmn:StartEvent",
                    Status = ExecutionStatus.Active,
                    ParentExecutionId = rootExecutionPath.ExecutionId
                };
                
                // Add the execution path to state
                state.ExecutionPaths.Add(startEventExecution);
                state.ActiveExecutions[startEventExecution.ExecutionId] = startEventExecution;
                
                // Map the start event to its execution path
                if (!state.ElementExecutionPaths.TryGetValue(startEvent.id, out var paths))
                {
                    paths = new List<string>();
                    state.ElementExecutionPaths[startEvent.id] = paths;
                }
                paths.Add(startEventExecution.ExecutionId);
                
                // Save updated state
                await _stateStore.SaveStateAsync(@event.ProcessInstanceId, state, 2);
                
                // Publish ElementCreated event
                var elementCreatedEvent = new ElementCreated
                {
                    ProcessInstanceId = @event.ProcessInstanceId,
                    ElementId = startEvent.id,
                    ElementType = "bpmn:StartEvent",
                    IsExecutable = true, // Start events are always executable
                    ExecutionId = startEventExecution.ExecutionId
                };
                
                await _eventBus.PublishAsync(elementCreatedEvent, cancellationToken);
                
                // Add the event to the execution path
                state.AddEventToExecution(startEventExecution.ExecutionId, elementCreatedEvent);
            }
            
            // Update statistics
            state.UpdateExecutionStatistics();
            await _stateStore.SaveStateAsync(@event.ProcessInstanceId, state, 4);
            
            _logger.LogInformation("Successfully activated {Count} start events for process instance {ProcessInstanceId}",
                startEvents.Count, @event.ProcessInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling ProcessInstanceCreated event for process instance {ProcessInstanceId}",
                @event.ProcessInstanceId);
            throw;
        }
    }
    
    private BpmnProcess FindProcess(BpmnDefinitions definitions, string processId)
    {
        if (definitions?.Items == null || !definitions.Items.Any())
            return null;
            
        var processes = definitions.Items
            .OfType<BpmnProcess>()
            .ToList();
            
        if (!processes.Any())
            return null;
            
        if (string.IsNullOrEmpty(processId))
            return processes.First();
            
        return processes.FirstOrDefault(p => p.id == processId);
    }
    
    private List<BpmnStartEvent> FindStartEvents(BpmnProcess process)
    {
        if (process?.Items == null || !process.Items.Any())
            return new List<BpmnStartEvent>();
            
        return process.Items
            .OfType<BpmnStartEvent>()
            .ToList();
    }
}