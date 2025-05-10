using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// Handles the completion of process instances and performs cleanup
/// </summary>
public class ProcessCompletedHandler : IBpmnEventHandler<ProcessCompletedEvent>
{
    private readonly ILogger<ProcessCompletedHandler> _logger;
    private readonly IStateStore _stateStore;
    private readonly IEventBus _eventBus;
    private readonly IBpmnDefinitionStorage _definitionStorage;
    
    public ProcessCompletedHandler(
        ILogger<ProcessCompletedHandler> logger,
        IStateStore stateStore,
        IEventBus eventBus,
        IBpmnDefinitionStorage definitionStorage)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _definitionStorage = definitionStorage ?? throw new ArgumentNullException(nameof(definitionStorage));
    }
    
    public async Task HandleAsync(ProcessCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }
        
        try
        {
            _logger.LogDebug("Processing ProcessInstanceCompleted event for process instance {ProcessInstanceId}", 
                @event.ProcessInstanceId);
            
            // Get current state
            var state = await _stateStore.GetStateAsync<BpmnProcessState>(@event.ProcessInstanceId);
            if (state == null)
            {
                throw new InvalidOperationException($"Process instance state not found for {@event.ProcessInstanceId}");
            }
            
            // Update process state
            state.Status = ProcessStatus.Completed;
            state.ActiveElements.Clear();
            
            // Save final state
            await _stateStore.SaveStateAsync(@event.ProcessInstanceId, state, 1);
            

            
            _logger.LogInformation("Successfully completed process instance {ProcessInstanceId}",
                @event.ProcessInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling ProcessInstanceCompleted event for process instance {ProcessInstanceId}",
                @event.ProcessInstanceId);
            throw;
        }
    }
} 