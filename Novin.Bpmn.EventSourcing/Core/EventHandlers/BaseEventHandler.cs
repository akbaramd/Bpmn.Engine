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
        public ElementExecution? Execution { get; set; }
        
        public EventHandlerContext(ProcessInstanceState state, ElementExecution? execution = null)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Execution = execution;
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
        
        // Use the new DistributedLockManager instead of static dictionary
        private static readonly DistributedLockManager _lockManager;
        
        // Static constructor to initialize the lock manager
        static BaseEventHandler()
        {
            var loggerFactory = new Microsoft.Extensions.Logging.LoggerFactory();
            var logger = loggerFactory.CreateLogger<DistributedLockManager>();
            _lockManager = new DistributedLockManager(logger);
        }

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

            // Use the async-compatible pattern with await using
            // If the lock can't be acquired in time, it will return a dummy releaser
            await using var lockReleaser = await _lockManager.AcquireLockAsync(
                @event.InstanceId, 
                TimeSpan.FromSeconds(10), 
                cancellationToken);

            // No need to track stateVersion anymore - we'll let the store handle versioning
            const int maxRetries = 5;
            int retryCount = 0;
            bool success = false;

            if (@event is ElementEvent elEv)
            {
                Logger.LogDebug("Handling element event {ElementType} for element {ElementId}", 
                    elEv.EventType, elEv.ElementId);
            }
            
            while (!success && retryCount <= maxRetries)
            {
                try
                {
                    // Load the process instance state
                    var stateWithVersion = await StateStore
                        .GetAsync(@event.InstanceId, cancellationToken);
                        
                    if (stateWithVersion == null)
                        throw new InvalidOperationException($"Process instance '{@event.InstanceId}' not found.");
                    
                    var state = stateWithVersion.Value.State;

                    Logger.LogInformation("⏳ Handling {EventType} for instance {InstanceId} (version {Version})", 
                        @event.EventType, @event.InstanceId, stateWithVersion.Value.Version);

                    // Record the event in the process history and find/create the appropriate execution
                    var context = await PrepareContextAsync(@event, state, cancellationToken);

                    // Process event in lifecycle hooks
                    await BeforeHandleAsync(@event, context, cancellationToken);
                    
                    // Ensure variables are synced
                    if (context.Execution != null)
                    {
                        SyncVariablesToExecution(context.State, context.Execution);
                    }

                    // Do the actual event processing
                    await ProcessEventAsync(@event, context, cancellationToken);
                    
                    // Ensure variables are synced again after processing
                    if (context.Execution != null)
                    {
                        SyncVariablesToExecution(context.State, context.Execution);
                    }

                    // Now that processing succeeded, finalize the state transition based on event type
                    FinalizeStateTransition(@event, context);

                    // Persist the state and event
                    // Always pass null for expected version to allow state merging
                    await StateStore.UpsertAsync(state, null, cancellationToken);
                    await EventStore.AppendEventAsync(@event, cancellationToken);

                    // Run after-handle hook
                    await AfterHandleAsync(@event, context, cancellationToken);

                    // Publish any pending events if there are any
                    if (_postEvents.Count > 0)
                    {
                        Logger.LogDebug("Publishing {Count} pending events", _postEvents.Count);
                        var tasks = new List<Task>(_postEvents.Count);
                        
                        foreach (var postEvent in _postEvents)
                        {
                            // Ensure all element events have proper execution ID if possible
                            if (postEvent is ElementEvent elementEvent && 
                                context.Execution != null && 
                                string.IsNullOrEmpty(elementEvent.ExecutionId) &&
                                elementEvent.ElementId == context.Execution.ElementId)
                            {
                                // Try to set the execution ID on the event
                                elementEvent.ExecutionId = context.Execution.ExecutionId;
                                Logger.LogDebug("Set execution ID {ExecutionId} on pending event {EventType}",
                                    context.Execution.ExecutionId, postEvent.GetType().Name);
                            }
                            
                            tasks.Add(_eventBus.PublishAsync(postEvent, cancellationToken));
                        }

                        await Task.WhenAll(tasks);
                        Logger.LogDebug("All {Count} pending events published successfully", _postEvents.Count);
                    }
                    else
                    {
                        Logger.LogDebug("No pending events to publish for {EventType}", @event.EventType);
                    }
                    
                    Logger.LogInformation("✅ Successfully handled {EventType} for instance {InstanceId}", @event.EventType, @event.InstanceId);
                    success = true;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Version mismatch") || 
                                                        ex.Message.Contains("Concurrency conflict"))
                {
                    retryCount++;
                    
                    if (retryCount > maxRetries)
                    {
                        Logger.LogError(ex, "❌ Failed to handle {EventType} for instance {InstanceId} after {MaxRetries} retries due to concurrency conflicts", 
                            @event.EventType, @event.InstanceId, maxRetries);
                        throw new InvalidOperationException(
                            $"Failed to handle {nameof(@event.EventType)} for instance {@event.InstanceId} after {maxRetries} retries due to concurrency conflicts", ex);
                    }
                    
                    Logger.LogWarning("Concurrency conflict detected for {EventType} on instance {InstanceId}, retrying {RetryCount}/{MaxRetries}", 
                        @event.EventType, @event.InstanceId, retryCount, maxRetries);
                        
                    // Wait with exponential backoff before retrying
                    int delay = Math.Min(100 * (int)Math.Pow(2, retryCount), 5000);
                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "❌ Error handling {EventType} for instance {InstanceId}", @event.EventType, @event.InstanceId);
                    throw;
                }
                finally
                {
                    if (success || retryCount > maxRetries)
                    {
                        _postEvents.Clear(); // Ensure clean state
                    }
                }
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
            
            // Initialize execution as null
            ElementExecution? execution = null;
            
            // If this is an element event, handle execution context
            if (@event is ElementEvent elementEvent && !string.IsNullOrEmpty(elementEvent.ElementId))
            {
                // Try to get execution ID from the event
                string executionId = elementEvent.ExecutionId ?? string.Empty;
                
                // If we have an execution ID, try to find the execution
                if (!string.IsNullOrEmpty(executionId))
                {
                    execution = state.GetExecution(executionId);
                    
                    if (execution != null)
                    {
                        Logger.LogDebug("Found execution by ID {ExecutionId} for element {ElementId}", 
                            executionId, elementEvent.ElementId);
                    }
                    else
                    {
                        Logger.LogWarning("Execution ID {ExecutionId} specified but not found for element {ElementId}", 
                            executionId, elementEvent.ElementId);
                    }
                }
                
                // If we still don't have an execution but have element ID, try to find by element ID
                if (execution == null)
                {
                    // Look for active executions for this element first
                    execution = state.ActiveExecutions
                        .FirstOrDefault(e => e.ElementId == elementEvent.ElementId);
                    
                    // If no active execution found, check all executions
                    if (execution == null && !(@event is ElementCreated))
                    {
                        execution = state.Executions.Values
                            .FirstOrDefault(e => e.ElementId == elementEvent.ElementId);
                    }
                    
                    if (execution != null)
                    {
                        Logger.LogDebug("Found execution by element ID {ElementId} with execution ID {ExecutionId}", 
                            elementEvent.ElementId, execution.ExecutionId);
                        
                        // Update the event's execution ID if possible
                        if (string.IsNullOrEmpty(elementEvent.ExecutionId))
                        {
                            var executionIdProp = elementEvent.GetType().GetProperty("ExecutionId");
                            if (executionIdProp?.SetMethod != null)
                            {
                                executionIdProp.SetValue(elementEvent, execution.ExecutionId);
                                Logger.LogDebug("Updated event execution ID to {ExecutionId}", execution.ExecutionId);
                            }
                        }
                    }
                }
                
                // Handle ElementCreated events
                if (@event is ElementCreated createdEvent)
                {
                    // Only create execution if one doesn't already exist for this element
                    if (execution == null)
                    {
                        // Create a new execution for this element
                        execution = ElementExecutionBuilder.Init()
                            .WithProcessInstanceId(state.InstanceId)
                            .WithElementId(elementEvent.ElementId)
                            .WithLocalVariables(state.Variables)
                            .WithElementType(elementEvent.ElementType)
                            .Executable(createdEvent.IsExecutable)
                            .Build()
                            .BuildResult();
                        
                        // Add the execution to the process state
                        state.AddExecution(execution);
                        
                        // Update the execution ID in the event
                        createdEvent.ExecutionId = execution.ExecutionId;
                        
                        Logger.LogDebug("Created new execution {ExecutionId} for element {ElementId}", 
                            execution.ExecutionId, elementEvent.ElementId);
                    }
                    else
                    {
                        Logger.LogWarning("ElementCreated event received but execution already exists for element {ElementId}", 
                            elementEvent.ElementId);
                    }
                }
                // For non-ElementCreated events where no execution was found
                else if (execution == null)
                {
                    Logger.LogWarning("No execution found for element {ElementId} with event {EventType}. Creating emergency execution.", 
                        elementEvent.ElementId, @event.GetType().Name);
                    
                    // Create an emergency execution
                    execution = ElementExecutionBuilder.Init()
                        .WithProcessInstanceId(state.InstanceId)
                        .WithElementId(elementEvent.ElementId)
                        .WithLocalVariables(state.Variables)
                        .WithElementType(elementEvent.ElementType)
                        .Build()
                        .BuildResult();
                    
                    // Add the execution to the process state
                    state.AddExecution(execution);
                    
                    // Update the execution ID in the event
                    elementEvent.ExecutionId = execution.ExecutionId;
                }
                
                // Add event to execution's history if we have an execution
                if (execution != null)
                {
                    execution.AddEvent(@event);
                }
            }
            
            // Create and return the context with state and execution
            var context = new EventHandlerContext(state, execution);
            return context;
        }
        
        /// <summary>
        /// Synchronizes variables from process state to execution
        /// </summary>
        private void SyncVariablesToExecution(ProcessInstanceState state, ElementExecution execution)
        {
            if (state?.Variables == null || !state.Variables.Any() || execution == null)
                return;
                
            foreach (var kvp in state.Variables)
            {
                // Only add variables that don't already exist in the execution
                if (!execution.LocalVariables.ContainsKey(kvp.Key))
                {
                    execution.LocalVariables[kvp.Key] = kvp.Value;
                }
            }
        }
        
        /// <summary>
        /// Finalizes the state transition after successful event processing.
        /// </summary>
        private void FinalizeStateTransition(TEvent @event, EventHandlerContext context)
        {
            var state = context.State;
            var execution = context.Execution;
            
            // If this is an element-related event, update the execution state
            if (@event is ElementEvent elementEvent && !string.IsNullOrEmpty(elementEvent.ElementId) &&
                execution != null)
            {
                try
                {
                    // First, always sync variables from execution to process instance
                    // This ensures we capture any changes made during event processing
                    state.SyncVariablesFromExecution(execution);
                    
                    // Now update execution state based on event type
                    if (@event is ElementCompleted)
                    {
                        // Complete the execution
                        execution.Complete();
                        
                        // Final sync of variables after completion
                        state.SyncVariablesFromExecution(execution);
                        
                        Logger.LogDebug("Element {ElementId} completed in execution {ExecutionId}", 
                            elementEvent.ElementId, execution.ExecutionId);
                    }
                    else if (@event is ElementFailed failed)
                    {
                        // Apply failure to the execution
                        execution.Fail(failed.ErrorMessage ?? "Unknown error");
                        
                        // Final sync of variables after failure
                        state.SyncVariablesFromExecution(execution);
                        
                        Logger.LogDebug("Element {ElementId} failed in execution {ExecutionId}: {Error}", 
                            elementEvent.ElementId, execution.ExecutionId, failed.ErrorMessage);
                    }
                    else if (@event is ElementTerminated terminated)
                    {
                        // Apply termination to the execution
                        execution.Terminate();
                        
                        // Final sync of variables after termination
                        state.SyncVariablesFromExecution(execution);
                        
                        Logger.LogDebug("Element {ElementId} terminated in execution {ExecutionId}", 
                            elementEvent.ElementId, execution.ExecutionId);
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't throw to allow further processing
                    Logger.LogError(ex, "Error during state transition for element {ElementId} execution {ExecutionId}",
                        elementEvent.ElementId, execution.ExecutionId);
                }
            }
            
            // For process lifecycle events
            try
            {
                if (@event is ProcessCompleted completed)
                {
                    state.Complete(completed);
                    Logger.LogDebug("Process {InstanceId} completed", state.InstanceId);
                }
                else if (@event is ProcessFailed failed)
                {
                    state.Fail(failed);
                    Logger.LogDebug("Process {InstanceId} failed: {ErrorMessage}", state.InstanceId, failed.ErrorMessage);
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
            }
            catch (Exception ex)
            {
                // Log but don't throw to allow further processing
                Logger.LogError(ex, "Error during process state transition for instance {InstanceId}", state.InstanceId);
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
