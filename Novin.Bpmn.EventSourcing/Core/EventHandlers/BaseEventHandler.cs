using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers
{
    /// <summary>
    /// Context for event handlers that contains both the process state and current execution
    /// </summary>
    public class EventHandlerContext
    {
        /// <summary>
        /// The current process instance state
        /// </summary>
        public ProcessInstanceState State { get; }
        
        /// <summary>
        /// The current execution context (may be null for process-level events)
        /// </summary>
        public ElementExecution? CurrentExecution { get; set; }
        
        public EventHandlerContext(ProcessInstanceState state, ElementExecution? execution = null)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            CurrentExecution = execution;
        }
    }
    
    /// <summary>
    /// Base class for all BPMN event handlers.
    /// Automatically loads state, invokes lifecycle hooks, persists state & events, and logs.
    /// </summary>
    /// <typeparam name="TEvent">Type of BPMN event to handle</typeparam>
    public abstract class BaseEventHandler<TEvent> : IBpmnEventHandler<TEvent>
        where TEvent : IBpmnEvent
    {
        protected readonly ILogger<BaseEventHandler<TEvent>> Logger;
        protected readonly IProcessInstanceStateStore StateStore;
        protected readonly IEventBus _eventBus;
        protected readonly IEventStore EventStore;
        protected readonly IProcessDeploymentStore DefinitionStore;
        private readonly List<IBpmnEvent> _postEvents = new();

        protected BaseEventHandler(
            IProcessInstanceStateStore stateStore,
            IEventStore eventStore,
            IProcessDeploymentStore definitionStore,
            IEventBus eventBus,
            ILogger<BaseEventHandler<TEvent>> logger)
        {
            StateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            EventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
            DefinitionStore = definitionStore ?? throw new ArgumentNullException(nameof(definitionStore));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <inheritdoc />
        public async Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default)
        {
            if (@event is null) throw new ArgumentNullException(nameof(@event));

            var state = await StateStore
                .GetAsync(@event.InstanceId, cancellationToken)
                ?? throw new InvalidOperationException($"Process instance '{@event.InstanceId}' not found.");

            Logger.LogInformation("⏳ Handling {EventType} for instance {InstanceId}", @event.EventType, @event.InstanceId);

            try
            {
                // Record the event in the process history and find/create the appropriate execution
                var context = await PrepareContextAsync(@event, state.State, cancellationToken);

                // Process event in lifecycle hooks
                await BeforeHandleAsync(@event, context, cancellationToken);

                // Do the actual event processing
                await ProcessEventAsync(@event, context, cancellationToken);

                // Now that processing succeeded, finalize the state transition based on event type
                FinalizeStateTransition(@event, context);

                // Persist the state and event
                await StateStore.UpsertAsync(state.State, ct: cancellationToken);
                await EventStore.AppendEventAsync(@event, cancellationToken);

                // Run after-handle hook
                await AfterHandleAsync(@event, context, cancellationToken);

                // Publish any pending events
                foreach (var postEvent in _postEvents)
                {
                    await _eventBus.PublishAsync(postEvent, cancellationToken);
                }

                Logger.LogInformation("✅ Successfully handled {EventType} for instance {InstanceId}", @event.EventType, @event.InstanceId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "❌ Error handling {EventType} for instance {InstanceId}", @event.EventType, @event.InstanceId);
                throw;
            }
            finally
            {
                _postEvents.Clear(); // Ensure clean state
            }
        }

        /// <summary>
        /// Records the event in the process history and links it to the appropriate execution.
        /// This doesn't finalize state transitions yet.
        /// </summary>
        private async Task<EventHandlerContext> PrepareContextAsync(
            TEvent @event,
            ProcessInstanceState state,
            CancellationToken cancellationToken)
        {
            // First, record the event in the process instance history
            state.RecordEvent(@event);
            
            // Create an initial context with just the state
            var context = new EventHandlerContext(state);
            
            // If this is an element event, handle execution context
            if (@event is ElementEvent elementEvent && !string.IsNullOrEmpty(elementEvent.ElementId))
            {
                string executionId = elementEvent.ExecutionId ?? string.Empty;
                
                // If we don't have an execution ID but have element info, try to find it
                if (string.IsNullOrEmpty(executionId) && !string.IsNullOrEmpty(elementEvent.ElementId))
                {
                    // Try to find an active execution for this element
                    var existingExecution = state.ActiveExecutions
                        .FirstOrDefault(e => e.ElementId == elementEvent.ElementId);
                    
                    executionId = existingExecution?.ExecutionId ?? string.Empty;
                }
                
                // For ElementCreated events, create a new execution
                if (@event is ElementCreated)
                {
                    // Create a new execution for this element
                    var execution = ElementExecutionBuilder.Init()
                        .WithProcessInstanceId(state.InstanceId)
                        .WithElementId(elementEvent.ElementId)
                        .WithElementType(elementEvent.ElementType)
                        .Build()
                        .BuildResult();
                    
                    // Add event to the execution's history
                    execution.AddEvent(@event);
                    
                    // Add the execution to the process state
                    state.AddExecution(execution);
                    
                    // Set the execution in the context
                    context.CurrentExecution = execution;
                    
                    // Update the execution ID in the event (if we have a setter)
                    if (elementEvent is ElementCreated created)
                    {
                        // Using reflection since we can't modify the event directly
                        var executionIdProp = created.GetType().GetProperty("ExecutionId");
                        if (executionIdProp?.SetMethod != null)
                        {
                            executionIdProp.SetValue(created, execution.ExecutionId);
                        }
                    }
                    
                    Logger.LogDebug("Created new execution {ExecutionId} for element {ElementId}", 
                        execution.ExecutionId, elementEvent.ElementId);
                }
                // For existing executions, just record the event
                else if (!string.IsNullOrEmpty(executionId))
                {
                    // For existing executions
                    var execution = state.GetExecution(executionId);
                    
                    // Add event to execution's history, but don't change state yet
                    execution.AddEvent(@event);
                    
                    // Set the execution in the context
                    context.CurrentExecution = execution;
                    
                    Logger.LogDebug("Found existing execution {ExecutionId} for element {ElementId}", 
                        execution.ExecutionId, elementEvent.ElementId);
                }
            }
            
            await Task.CompletedTask;
            return context;
        }
        
        /// <summary>
        /// Finalizes the state transition after successful event processing.
        /// </summary>
        private void FinalizeStateTransition(TEvent @event, EventHandlerContext context)
        {
            var state = context.State;
            var execution = context.CurrentExecution;
            
            // If this is an element-related event, update the execution state
            if (@event is ElementEvent elementEvent && !string.IsNullOrEmpty(elementEvent.ElementId) &&
                execution != null)
            {
                // Update execution state based on event type
                if (@event is ElementCompleted)
                {
                    // Complete the execution
                    execution.Complete();
                    
                    // Sync variables from execution to process instance
                    state.SyncVariablesFromExecution(execution);
                    
                    Logger.LogDebug("Element {ElementId} completed in execution {ExecutionId}", 
                        elementEvent.ElementId, execution.ExecutionId);
                }
                else if (@event is ElementFailed failed)
                {
                    // Apply failure to the execution
                    execution.Fail(failed.ErrorMessage ?? "Unknown error");
                    
                    // Sync variables from execution to process instance
                    state.SyncVariablesFromExecution(execution);
                    
                    Logger.LogDebug("Element {ElementId} failed in execution {ExecutionId}: {Error}", 
                        elementEvent.ElementId, execution.ExecutionId, failed.ErrorMessage);
                }
                else if (@event is ElementTerminated)
                {
                    // Apply termination to the execution
                    execution.Terminate();
                    
                    // Sync variables from execution to process instance
                    state.SyncVariablesFromExecution(execution);
                    
                    Logger.LogDebug("Element {ElementId} terminated in execution {ExecutionId}", 
                        elementEvent.ElementId, execution.ExecutionId);
                }
            }
            
            // For process lifecycle events
            if (@event is ProcessCompleted completed)
            {
                state.Complete(completed);
                Logger.LogDebug("Process {InstanceId} completed", state.InstanceId);
            }
            else if (@event is ProcessFailed failed)
            {
                state.Fail(failed);
                Logger.LogDebug("Process {InstanceId} failed", state.InstanceId);
            }
            else if (@event is ProcessTerminated terminated)
            {
                state.Terminate(terminated);
                Logger.LogDebug("Process {InstanceId} terminated", state.InstanceId);
            }
            else if (@event is ProcessSuspended suspended)
            {
                state.Suspend(suspended);
                Logger.LogDebug("Process {InstanceId} suspended", state.InstanceId);
            }
            else if (@event is ProcessResumed resumed)
            {
                state.Resume(resumed);
                Logger.LogDebug("Process {InstanceId} resumed", state.InstanceId);
            }
            else if (@event is ProcessCancelled cancelled)
            {
                state.Cancel(cancelled);
                Logger.LogDebug("Process {InstanceId} cancelled", state.InstanceId);
            }
            
            // Always touch to update the timestamp
            state.Touch();
        }

        /// <summary>
        /// Override to run logic before <see cref="ProcessEventAsync"/>.
        /// </summary>
        protected virtual Task BeforeHandleAsync(
            TEvent @event,
            EventHandlerContext context,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        /// <summary>
        /// Must implement the core event‐driven state mutation logic here.
        /// </summary>
        protected abstract Task ProcessEventAsync(
            TEvent @event,
            EventHandlerContext context,
            CancellationToken cancellationToken);

        /// <summary>
        /// Override to run logic after state & event are persisted.
        /// </summary>
        protected virtual Task AfterHandleAsync(
            TEvent @event,
            EventHandlerContext context,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        /// <summary>
        /// Get BPMN definitions for a deployment
        /// </summary>
        protected virtual Task<BpmnDefinitions?> GetDefinitionsAsync(
            Guid deploymentId,
            CancellationToken cancellationToken) =>
            DefinitionStore.GetDefinitionsAsync(deploymentId, cancellationToken);

        /// <summary>
        /// Get deployment state information
        /// </summary>
        protected virtual Task<ProcessDeploymentState?> GetDeploymentAsync(
            Guid deploymentId,
            CancellationToken cancellationToken) =>
            DefinitionStore.GetDeploymentAsync(deploymentId, cancellationToken);

        /// <summary>
        /// Get a definition explorer for navigating BPMN definitions
        /// </summary>
        protected virtual DefiantionExplorer GetDefiantionExplorer(BpmnDefinitions definitions) =>
            new DefiantionExplorer(definitions);

        /// <summary>
        /// Schedule an event to be published after the current event is processed
        /// </summary>
        protected void PublishLater(IBpmnEvent @event)
        {
            if (@event == null) throw new ArgumentNullException(nameof(@event));
            _postEvents.Add(@event);
        }

        /// <summary>
        /// Schedule multiple events to be published after the current event is processed
        /// </summary>
        protected void PublishLater(IEnumerable<IBpmnEvent> events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            _postEvents.AddRange(events);
        }
    }
}
