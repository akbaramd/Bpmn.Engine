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
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Text.RegularExpressions;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// Handles the initial creation and activation of BPMN elements
/// Manages gateway merges and transitions to processing state
/// </summary>
public class ElementCreatedHandler : IBpmnEventHandler<ElementCreated>
{
    private readonly ILogger<ElementCreatedHandler> _logger;
    private readonly IStateStore _stateStore;
    private readonly IEventBus _eventBus;
    private readonly IBpmnDefinitionStorage _definitionStorage;
    
    private const int MaxRetries = 3;
    private const int RetryDelay = 1000;

    /// <summary>
    /// Creates a new instance of ElementCreatedHandler
    /// </summary>
    public ElementCreatedHandler(
        ILogger<ElementCreatedHandler> logger,
        IStateStore stateStore,
        IEventBus eventBus,
        IBpmnDefinitionStorage definitionStorage)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _definitionStorage = definitionStorage ?? throw new ArgumentNullException(nameof(definitionStorage));
    }
    
    /// <inheritdoc />
    public async Task HandleAsync(ElementCreated @event, CancellationToken cancellationToken = default)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }
        
        _logger.LogInformation("Handling ElementCreated event for process instance {ProcessInstanceId}, element {ElementId}, IsExecutable: {@IsExecutable}",
            @event.ProcessInstanceId, @event.ElementId, @event.IsExecutable);

        try
        {
            // Get current state with retry
            var (state, version) = await GetStateWithRetryAsync(@event.ProcessInstanceId, cancellationToken);
            if (state == null)
            {
                _logger.LogError("Process state not found for instance {ProcessInstanceId}",
                    @event.ProcessInstanceId);
                return;
            }
            
            // Get BPMN definition using DeploymentKey or ProcessDefinitionId from state
            var bpmnDefinition = _definitionStorage.GetParsedDefinition(state.DeploymentKey ?? state.ProcessDefinitionId);
            if (bpmnDefinition == null)
            {
                _logger.LogError("BPMN definition not found for process instance {ProcessInstanceId}",
                    @event.ProcessInstanceId);
                return;
            }

            // Create execution path for this element
            string executionId = await CreateExecutionPathAsync(state, @event, cancellationToken);

            // Update state
            state.ActiveElements.Add(@event.ElementId);
            state.ElementStatuses[@event.ElementId] = new ElementStatus
            {
                ElementId = @event.ElementId,
                ElementType = @event.ElementType,
                Status = "Created",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            // Add the event to the execution
            if (!string.IsNullOrEmpty(executionId))
            {
                state.AddEventToExecution(executionId, @event);
            }
            
            // Save state with retry
            await SaveStateWithRetryAsync(@event.ProcessInstanceId, state, (int)version, cancellationToken);
            
            // Handle different element types first before deciding execution path
            switch (@event.ElementType)
            {
                case "bpmn:StartEvent":
                    await HandleStartEventCreatedAsync(state, @event, executionId, cancellationToken);
                    break;
                    
                case "bpmn:EndEvent":
                    await HandleEndEventCreatedAsync(state, @event, executionId, cancellationToken);
                    break;
                    
                case "bpmn:UserTask":
                case "bpmn:ServiceTask":
                case "bpmn:ScriptTask":
                case "bpmn:BusinessRuleTask":
                case "bpmn:ManualTask":
                case "bpmn:ReceiveTask":
                case "bpmn:SendTask":
                    await HandleTaskCreatedAsync(state, @event, executionId, cancellationToken);
                    break;
                    
                case "bpmn:ParallelGateway":
                    await HandleParallelGatewayCreatedAsync(state, @event, executionId, cancellationToken);
                    return; // Return early as gateway handler will publish appropriate events
                    
                case "bpmn:InclusiveGateway":
                    await HandleInclusiveGatewayCreatedAsync(state, @event, executionId, cancellationToken);
                    return; // Return early as gateway handler will publish appropriate events
                    
                case "bpmn:ExclusiveGateway":
                    await HandleExclusiveGatewayCreatedAsync(state, @event, executionId, cancellationToken);
                    return; // Return early as gateway handler will publish appropriate events
                    
                case "bpmn:EventBasedGateway":
                    await HandleEventBasedGatewayCreatedAsync(state, @event, executionId, cancellationToken);
                    return; // Return early as gateway handler will publish appropriate events
                    
                case "bpmn:SubProcess":
                    await HandleSubProcessCreatedAsync(state, @event, executionId, cancellationToken);
                    break;
                    
                case "bpmn:CallActivity":
                    await HandleCallActivityCreatedAsync(state, @event, executionId, cancellationToken);
                    break;
                    
                default:
                    _logger.LogDebug("Element {ElementId} of type {ElementType} created in process {ProcessInstanceId}", 
                        @event.ElementId, @event.ElementType, @event.ProcessInstanceId);
                    break;
            }
            
            // After handling specific element types, determine executable path
            if (!@event.IsExecutable)
            {
                _logger.LogDebug("Element {ElementId} is marked as non-executable. Skipping processing and moving to completed state.",
                    @event.ElementId);
                
                // Update execution path status
                if (!string.IsNullOrEmpty(executionId) && 
                    state.ActiveExecutions.TryGetValue(executionId, out var execution))
                {
                    execution.Status = ExecutionStatus.Completed;
                    execution.IsExecutable = false;
                    
                    // Move from active to completed
                    state.ActiveExecutions.Remove(executionId);
                    
                    // Update statistics
                    state.UpdateExecutionStatistics();
                    
                    // Save updated execution state
                    await SaveStateWithRetryAsync(@event.ProcessInstanceId, state, (int)version + 1, cancellationToken);
                }
                
                // Publish ElementCompleted event directly
                await _eventBus.PublishAsync(new ElementCompleted
                {
                    EventId = Guid.NewGuid(),
                    ProcessInstanceId = @event.ProcessInstanceId,
                    ElementId = @event.ElementId,
                    ElementType = @event.ElementType,
                    Timestamp = DateTime.UtcNow,
                    ExecutionId = executionId,
                    IsExecutable = false // Keep the non-executable flag
                }, cancellationToken);
            }
            else
            {
                // Element is executable, publish processing event
                await _eventBus.PublishAsync(new ElementProcessing
                {
                    EventId = Guid.NewGuid(),
                    ProcessInstanceId = @event.ProcessInstanceId,
                    ElementId = @event.ElementId,
                    ElementType = @event.ElementType,
                    Progress = 0,
                    ProcessingDetails = "Element created, starting processing",
                    Timestamp = DateTime.UtcNow,
                    ExecutionId = executionId // Pass the execution ID
                }, cancellationToken);
            }
            
            _logger.LogInformation("Successfully handled ElementCreated event for element {ElementId}", @event.ElementId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling ElementCreated event for element {ElementId}", @event.ElementId);
            throw;
        }
    }

    /// <summary>
    /// Creates an execution path for the element
    /// </summary>
    private async Task<string> CreateExecutionPathAsync(
        BpmnProcessState state,
        ElementCreated @event,
        CancellationToken cancellationToken)
    {
        // Get source element and sequence flow info from event
        string sourceElementId = @event.SourceElementId ?? "start";
        string sourceElementType = string.IsNullOrEmpty(@event.SourceElementId) ? "bpmn:StartEvent" : "unknown";
        string sequenceFlowId = @event.SequenceFlowId;
        
        // Create a new execution path
        var executionPath = new ExecutionPath
        {
            SourceElementId = sourceElementId,
            SourceElementType = sourceElementType,
            TargetElementId = @event.ElementId,
            TargetElementType = @event.ElementType,
            SequenceFlowId = sequenceFlowId,
            Timestamp = @event.Timestamp,
            Status = ExecutionStatus.Active
        };
        
        // Add to state
        state.ExecutionPaths.Add(executionPath);
        state.ActiveExecutions[executionPath.ExecutionId] = executionPath;
        
        // Add to element execution paths tracking
        if (!state.ElementExecutionPaths.TryGetValue(@event.ElementId, out var executions))
        {
            executions = new List<string>();
            state.ElementExecutionPaths[@event.ElementId] = executions;
        }
        executions.Add(executionPath.ExecutionId);
        
        // Map sequence flow if present
        if (!string.IsNullOrEmpty(sequenceFlowId))
        {
            if (!state.ElementToSequenceFlows.TryGetValue(sourceElementId, out var flows))
            {
                flows = new List<string>();
                state.ElementToSequenceFlows[sourceElementId] = flows;
            }
            
            if (!flows.Contains(sequenceFlowId))
            {
                flows.Add(sequenceFlowId);
            }
        }
        
        return executionPath.ExecutionId;
    }
    
    private async Task<(BpmnProcessState? State, long Version)> GetStateWithRetryAsync(
        string processInstanceId,
        CancellationToken cancellationToken)
    {
        int retryCount = 0;
        while (true)
        {
            try
            {
                return await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(processInstanceId, cancellationToken);
            }
            catch (Exception ex) when (ex.Message.Contains("Concurrency conflict") && retryCount < MaxRetries)
            {
                retryCount++;
                _logger.LogWarning("Concurrency conflict detected while getting state, retry {RetryCount} of {MaxRetries}",
                    retryCount, MaxRetries);
                await Task.Delay(RetryDelay * retryCount, cancellationToken);
            }
        }
    }

    private async Task SaveStateWithRetryAsync(
        string processInstanceId,
        BpmnProcessState state,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        int retryCount = 0;
        while (true)
        {
            try
            {
                await _stateStore.SaveStateAsync(processInstanceId, state, expectedVersion, cancellationToken);
                return;
            }
            catch (Exception ex) when (ex.Message.Contains("Concurrency conflict") && retryCount < MaxRetries)
            {
                retryCount++;
                _logger.LogWarning("Concurrency conflict detected while saving state, retry {RetryCount} of {MaxRetries}",
                    retryCount, MaxRetries);
                await Task.Delay(RetryDelay * retryCount, cancellationToken);

                // Get the latest state and version for the next retry
                var (latestState, latestVersion) = await GetStateWithRetryAsync(processInstanceId, cancellationToken);
                if (latestState != null)
                {
                    state = latestState;
                    expectedVersion = (int)latestVersion;
                }
            }
        }
    }
    
    private async Task HandleStartEventCreatedAsync(
        BpmnProcessState state,
        ElementCreated @event,
        string executionId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing creation of start event {EventId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        await TransitionToProcessingAsync(state, @event, executionId, cancellationToken);
    }
    
    private async Task HandleEndEventCreatedAsync(
        BpmnProcessState state,
        ElementCreated @event,
        string executionId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing creation of end event {EventId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        await TransitionToProcessingAsync(state, @event, executionId, cancellationToken);
    }
    
    private async Task HandleTaskCreatedAsync(
        BpmnProcessState state,
        ElementCreated @event,
        string executionId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing creation of task {TaskId} of type {TaskType} in process {ProcessInstanceId}",
            @event.ElementId, @event.ElementType, @event.ProcessInstanceId);
            
        await TransitionToProcessingAsync(state, @event, executionId, cancellationToken);
    }
    
    private async Task HandleParallelGatewayCreatedAsync(
        BpmnProcessState state,
        ElementCreated @event,
        string executionId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing creation of parallel gateway {GatewayId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        var gatewayInfo = await GetGatewayInfoAsync(state, @event.ElementId, cancellationToken);
        
        // Track execution count for this element
        if (!state.ElementExecutionCounts.TryGetValue(@event.ElementId, out var execCount))
        {
            execCount = 0;
        }
        state.ElementExecutionCounts[@event.ElementId] = execCount + 1;
        
        if (gatewayInfo.IsJoin)
        {
            // Initialize or update gateway merge info
            var mergeInfo = await InitializeGatewayMergeInfoAsync(state, @event, gatewayInfo, "bpmn:ParallelGateway", cancellationToken);
            
            // For join gateways, check if we can proceed
            bool canMerge = mergeInfo.CanMerge;
            
            if (canMerge)
            {
                _logger.LogDebug("Parallel gateway {GatewayId} can proceed with merge in process {ProcessInstanceId}. Received {Received} of {Required} flows.",
                    @event.ElementId, @event.ProcessInstanceId, mergeInfo.ReceivedIncomingFlows, mergeInfo.RequiredIncomingFlows);
                    
                // Remove all incoming flow tokens before proceeding
                foreach (var flowId in gatewayInfo.IncomingFlows)
                {
                    state.CompletedElements.Remove(flowId);
                }
                
                // Save state after removing tokens
                var (_, currentVersion) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
                await _stateStore.SaveStateAsync(@event.ProcessInstanceId, state, currentVersion);
                
                await TransitionToProcessingAsync(state, @event, executionId, cancellationToken);
                
                // Reset gateway state for potential reuse
                state.GatewayMergeStates.Remove(@event.ElementId);
            }
            else
            {
                _logger.LogDebug("Parallel gateway {GatewayId} is waiting for more tokens in process {ProcessInstanceId}. Current: {Current}, Required: {Required}",
                    @event.ElementId, @event.ProcessInstanceId, mergeInfo.ReceivedIncomingFlows, mergeInfo.RequiredIncomingFlows);
                    
                // Update gateway merge info in state
                var (_, currentVersion) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
                await _stateStore.SaveStateAsync(@event.ProcessInstanceId, state, currentVersion);
            }
        }
        else
        {
            // For split gateways, proceed to processing
            await TransitionToProcessingAsync(state, @event, executionId, cancellationToken);
        }
    }
    
    /// <summary>
    /// Handles creation of an inclusive gateway and sets up proper merging/splitting
    /// </summary>
    private async Task HandleInclusiveGatewayCreatedAsync(
        BpmnProcessState state,
        ElementCreated @event,
        string executionId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing creation of inclusive gateway {GatewayId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        var gatewayInfo = await GetGatewayInfoAsync(state, @event.ElementId, cancellationToken);
        
        // Track execution count for this element
        if (!state.ElementExecutionCounts.TryGetValue(@event.ElementId, out var execCount))
        {
            execCount = 0;
        }
        state.ElementExecutionCounts[@event.ElementId] = execCount + 1;
        
        if (gatewayInfo.IsJoin)
        {
            // Handle Join Gateway - merge flows
            await HandleInclusiveJoinGatewayAsync(state, @event, gatewayInfo, executionId, cancellationToken);
        }
        else
        {
            // For split gateways, we just pass to processing
            // Conditions will be evaluated later in ElementCompletedHandler
            await TransitionToProcessingAsync(state, @event, executionId, cancellationToken);
        }
    }

    /// <summary>
    /// Handle inclusive gateway when it acts as a join (has multiple incoming flows)
    /// </summary>
    private async Task HandleInclusiveJoinGatewayAsync(
        BpmnProcessState state,
        ElementCreated @event,
        GatewayInfo gatewayInfo,
        string executionId,
        CancellationToken cancellationToken)
    {
        // Get active incoming flows
        var activeFlows = await GetActiveIncomingFlowsAsync(state, @event.ElementId, cancellationToken);
        
        // Initialize or update gateway merge info
        var mergeInfo = await InitializeGatewayMergeInfoAsync(state, @event, gatewayInfo, "bpmn:InclusiveGateway", cancellationToken);
        
        // For join gateways, check if we can proceed
        bool canMerge = mergeInfo.CanMerge;
        
        // Check if any of the incoming paths were executable
        bool hasExecutablePath = mergeInfo.ReceivedFlowIds.Any(flowId => 
        {
            var execution = state.ExecutionPaths.FirstOrDefault(e => 
                e.SequenceFlowId == flowId && e.TargetElementId == @event.ElementId);
            return execution != null && execution.IsExecutable;
        });
        
        // We can only proceed with merge if we have at least one executable path
        bool shouldContinueExecution = canMerge && hasExecutablePath;
        
        if (canMerge)
        {
            _logger.LogDebug("Inclusive gateway {GatewayId} can proceed with merge in process {ProcessInstanceId}. Received {Received} of {Required} flows. Has executable path: {HasExecutablePath}",
                @event.ElementId, @event.ProcessInstanceId, mergeInfo.ReceivedIncomingFlows, mergeInfo.RequiredIncomingFlows, hasExecutablePath);
                
            // Remove tokens from active incoming flows
            foreach (var flowId in activeFlows)
            {
                state.CompletedElements.Remove(flowId);
            }
            
            // Save state after removing tokens
            var (_, currentVersion) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
            await _stateStore.SaveStateAsync(@event.ProcessInstanceId, state, currentVersion);
            
            // Update the execution path with executable status
            if (state.ActiveExecutions.TryGetValue(executionId, out var execution))
            {
                execution.IsExecutable = shouldContinueExecution;
            }
            
            if (shouldContinueExecution)
            {
                // Process normally if we have executable paths
                await TransitionToProcessingAsync(state, @event, executionId, cancellationToken);
            }
            else
            {
                // Complete without processing if no executable paths
                await _eventBus.PublishAsync(new ElementCompleted
                {
                    EventId = Guid.NewGuid(),
                    ProcessInstanceId = @event.ProcessInstanceId,
                    ElementId = @event.ElementId,
                    ElementType = @event.ElementType,
                    Timestamp = DateTime.UtcNow,
                    ExecutionId = executionId,
                    IsExecutable = false // Mark as non-executable
                }, cancellationToken);
            }
            
            // Reset gateway state for potential reuse
            state.GatewayMergeStates.Remove(@event.ElementId);
        }
        else
        {
            _logger.LogDebug("Inclusive gateway {GatewayId} is waiting for more tokens in process {ProcessInstanceId}. Current: {Current}, Required: {Required}",
                @event.ElementId, @event.ProcessInstanceId, mergeInfo.ReceivedIncomingFlows, mergeInfo.RequiredIncomingFlows);
                
            // Update gateway merge info in state
            var (_, currentVersion) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
            await _stateStore.SaveStateAsync(@event.ProcessInstanceId, state, currentVersion);
        }
    }
    
    private async Task HandleExclusiveGatewayCreatedAsync(
        BpmnProcessState state,
        ElementCreated @event,
        string executionId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing creation of exclusive gateway {GatewayId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        var gatewayInfo = await GetGatewayInfoAsync(state, @event.ElementId, cancellationToken);
        
        // Track execution count for this element
        if (!state.ElementExecutionCounts.TryGetValue(@event.ElementId, out var execCount))
        {
            execCount = 0;
        }
        state.ElementExecutionCounts[@event.ElementId] = execCount + 1;
        
        if (gatewayInfo.IsJoin)
        {
            // Initialize or update gateway merge info
            var mergeInfo = await InitializeGatewayMergeInfoAsync(state, @event, gatewayInfo, "bpmn:ExclusiveGateway", cancellationToken);
            
            // For XOR-join gateways, check if we have at least one token
            bool canMerge = mergeInfo.CanMerge;
            
            if (canMerge)
            {
                _logger.LogDebug("Exclusive gateway {GatewayId} has received a token, proceeding with merge in process {ProcessInstanceId}",
                    @event.ElementId, @event.ProcessInstanceId);
                    
                // Remove the token before proceeding
                state.CompletedElements.Remove(@event.ElementId);
                
                // Save state after removing token
                var (_, currentVersion) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
                await _stateStore.SaveStateAsync(@event.ProcessInstanceId, state, currentVersion);
                
                await TransitionToProcessingAsync(state, @event, executionId, cancellationToken);
                
                // Reset gateway state for potential reuse
                state.GatewayMergeStates.Remove(@event.ElementId);
            }
            else
            {
                _logger.LogDebug("Exclusive gateway {GatewayId} is waiting for a token in process {ProcessInstanceId}",
                    @event.ElementId, @event.ProcessInstanceId);
                    
                // Update gateway merge info in state
                var (_, currentVersion) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
                await _stateStore.SaveStateAsync(@event.ProcessInstanceId, state, currentVersion);
            }
        }
        else
        {
            // For split gateways, proceed to processing
            await TransitionToProcessingAsync(state, @event, executionId, cancellationToken);
        }
    }
    
    private async Task HandleEventBasedGatewayCreatedAsync(
        BpmnProcessState state,
        ElementCreated @event,
        string executionId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing creation of event-based gateway {GatewayId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        // Track execution count for this element
        if (!state.ElementExecutionCounts.TryGetValue(@event.ElementId, out var execCount))
        {
            execCount = 0;
        }
        state.ElementExecutionCounts[@event.ElementId] = execCount + 1;
        
        await TransitionToProcessingAsync(state, @event, executionId, cancellationToken);
    }
    
    /// <summary>
    /// Initialize or update gateway merge info based on gateway type and configuration
    /// </summary>
    private async Task<GatewayMergeInfo> InitializeGatewayMergeInfoAsync(
        BpmnProcessState state,
        ElementCreated @event,
        GatewayInfo gatewayInfo,
        string gatewayType,
        CancellationToken cancellationToken)
    {
        if (!state.GatewayMergeStates.TryGetValue(@event.ElementId, out var mergeInfo))
        {
            int requiredFlows = 1; // Default for exclusive gateway
            
            if (gatewayType == "bpmn:ParallelGateway")
            {
                // Parallel gateways need all incoming flows
                requiredFlows = gatewayInfo.IncomingFlows.Count();
            }
            else if (gatewayType == "bpmn:InclusiveGateway")
            {
                // Inclusive gateways need active flows
                var activeFlows = await GetActiveIncomingFlowsAsync(state, @event.ElementId, cancellationToken);
                requiredFlows = Math.Max(1, activeFlows.Count);
            }
            
            mergeInfo = new GatewayMergeInfo
            {
                GatewayId = @event.ElementId,
                GatewayType = gatewayType,
                RequiredIncomingFlows = requiredFlows,
                IncomingFlowIds = gatewayInfo.IncomingFlows.ToList()
            };
            
            state.GatewayMergeStates[@event.ElementId] = mergeInfo;
        }
        
        // For inclusive gateways, we need to update the required flow count on each check
        if (gatewayType == "bpmn:InclusiveGateway")
        {
            var activeFlows = await GetActiveIncomingFlowsAsync(state, @event.ElementId, cancellationToken);
            mergeInfo.RequiredIncomingFlows = Math.Max(1, activeFlows.Count);
        }
        
        // Record flow receipt if we know which sequence flow triggered this
        if (!string.IsNullOrEmpty(@event.SequenceFlowId))
        {
            mergeInfo.RecordFlowReceived(@event.SequenceFlowId);
            
            // Also track if this flow was executable
            mergeInfo.RecordFlowExecutableStatus(@event.SequenceFlowId, @event.IsExecutable);
        }
        
        return mergeInfo;
    }
    
    private async Task HandleSubProcessCreatedAsync(
        BpmnProcessState state,
        ElementCreated @event,
        string executionId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing creation of subprocess {SubProcessId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        await TransitionToProcessingAsync(state, @event, executionId, cancellationToken);
    }
    
    private async Task HandleCallActivityCreatedAsync(
        BpmnProcessState state,
        ElementCreated @event,
        string executionId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing creation of call activity {CallActivityId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        await TransitionToProcessingAsync(state, @event, executionId, cancellationToken);
    }
    
    private async Task TransitionToProcessingAsync(
        BpmnProcessState state,
        ElementCreated @event,
        string executionId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Transitioning element {ElementId} to processing state in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        await _eventBus.PublishAsync(new ElementProcessing
        {
            ProcessInstanceId = @event.ProcessInstanceId,
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            Progress = 0,
            ProcessingDetails = "Initial processing",
            ExecutionId = executionId // Pass the execution ID
        }, cancellationToken);
    }
    
    private async Task<GatewayInfo> GetGatewayInfoAsync(
        BpmnProcessState state,
        string gatewayId,
        CancellationToken cancellationToken)
    {
        var definitions = _definitionStorage.GetParsedDefinition(state.DeploymentKey);
        if (definitions == null)
        {
            _logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", state.DeploymentKey);
            return new GatewayInfo 
            { 
                Id = gatewayId,
                IsJoin = false,
                IncomingFlows = new List<string>(),
                OutgoingFlows = new List<string>()
            };
        }

        var process = FindProcess(definitions, state.ProcessDefinitionId);
        if (process == null)
        {
            _logger.LogWarning("Process definition not found with ID {ProcessId} in deployment {DeploymentKey}", 
                state.ProcessDefinitionId, state.DeploymentKey);
            return new GatewayInfo 
            { 
                Id = gatewayId,
                IsJoin = false,
                IncomingFlows = new List<string>(),
                OutgoingFlows = new List<string>()
            };
        }

        var gateway = FindElementById(process, gatewayId) as BpmnGateway;
        if (gateway == null)
        {
            _logger.LogWarning("Gateway element not found with ID {GatewayId} in process {ProcessId}", 
                gatewayId, state.ProcessDefinitionId);
            return new GatewayInfo 
            { 
                Id = gatewayId,
                IsJoin = false,
                IncomingFlows = new List<string>(),
                OutgoingFlows = new List<string>()
            };
        }

        var incomingFlows = FindIncomingFlows(process, gatewayId);
        var outgoingFlows = FindOutgoingFlows(process, gatewayId);
        bool isJoin = incomingFlows.Count > 1;

        return new GatewayInfo
        {
            Id = gatewayId,
            IsJoin = isJoin,
            IncomingFlows = incomingFlows.Select(f => f.id).ToList(),
            OutgoingFlows = outgoingFlows.Select(f => f.id).ToList()
        };
    }
    
    private async Task<List<string>> GetActiveIncomingFlowsAsync(
        BpmnProcessState state,
        string gatewayId,
        CancellationToken cancellationToken)
    {
        var definitions = _definitionStorage.GetParsedDefinition(state.DeploymentKey);
        if (definitions == null)
        {
            _logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", state.DeploymentKey);
            return new List<string>();
        }
        
        var process = FindProcess(definitions, state.ProcessDefinitionId);
        if (process == null)
        {
            _logger.LogWarning("Process definition not found with ID {ProcessId} in deployment {DeploymentKey}", 
                state.ProcessDefinitionId, state.DeploymentKey);
            return new List<string>();
        }
        
        var incomingFlows = FindIncomingFlows(process, gatewayId);
        var activeFlows = new List<string>();
        
        foreach (var flow in incomingFlows)
        {
            // Check if the source element is completed
            if (state.CompletedElements.Contains(flow.sourceRef))
            {
                activeFlows.Add(flow.id);
            }
        }
        
        return activeFlows;
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

    private BpmnBaseElement FindElementById(BpmnProcess process, string elementId)
    {
        if (process?.Items == null || !process.Items.Any() || string.IsNullOrEmpty(elementId))
            return null;

        foreach (var item in process.Items)
        {
            if (item is BpmnBaseElement element && element.id == elementId)
                return element;
                
            if (item is BpmnSubProcess subProcess && subProcess.Items != null)
            {
                var subElement = FindElementInSubProcess(subProcess, elementId);
                if (subElement != null)
                    return subElement;
            }
        }
        
        var flows = process.Items.OfType<BpmnSequenceFlow>().ToList();
        return flows.FirstOrDefault(f => f.id == elementId);
    }

    private BpmnBaseElement FindElementInSubProcess(BpmnSubProcess subProcess, string elementId)
    {
        if (subProcess?.Items == null || !subProcess.Items.Any())
            return null;
            
        foreach (var item in subProcess.Items)
        {
            if (item is BpmnBaseElement element && element.id == elementId)
                return element;
        }
        
        return null;
    }

    private List<BpmnSequenceFlow> FindIncomingFlows(BpmnProcess process, string elementId)
    {
        if (process?.Items == null || !process.Items.Any() || string.IsNullOrEmpty(elementId))
            return new List<BpmnSequenceFlow>();
            
        var flows = process.Items.OfType<BpmnSequenceFlow>().ToList();
        return flows.Where(f => f.targetRef == elementId).ToList();
    }

    private List<BpmnSequenceFlow> FindOutgoingFlows(BpmnProcess process, string elementId)
    {
        if (process?.Items == null || !process.Items.Any() || string.IsNullOrEmpty(elementId))
            return new List<BpmnSequenceFlow>();
            
        var flows = process.Items.OfType<BpmnSequenceFlow>().ToList();
        return flows.Where(f => f.sourceRef == elementId).ToList();
    }
    
    private class GatewayInfo
    {
        public string Id { get; set; }
        public bool IsJoin { get; set; }
        public IEnumerable<string> IncomingFlows { get; set; }
        public IEnumerable<string> OutgoingFlows { get; set; }
    }

    /// <summary>
    /// Check if all required incoming flows have been received for a merge gateway
    /// </summary>
    private bool HasReceivedAllRequiredFlows(GatewayMergeInfo mergeInfo, GatewayInfo gatewayInfo)
    {
        switch (mergeInfo.GatewayType)
        {
            case "bpmn:ParallelGateway":
                // Parallel gateway needs all incoming flows
                return mergeInfo.ReceivedIncomingFlows >= gatewayInfo.IncomingFlows.Count();
                
            case "bpmn:InclusiveGateway":
                // Inclusive gateway needs all active incoming flows
                return mergeInfo.ReceivedIncomingFlows >= mergeInfo.RequiredIncomingFlows;
                
            case "bpmn:ExclusiveGateway":
                // XOR gateway needs only one flow
                return mergeInfo.ReceivedIncomingFlows >= 1;
                
            default:
                return mergeInfo.ReceivedIncomingFlows >= 1;
        }
    }

    /// <summary>
    /// Evaluates a condition expression using C# scripting
    /// </summary>
    /// <param name="state">Process state containing variables</param>
    /// <param name="condition">The condition to evaluate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of condition evaluation (true/false)</returns>
    private async Task<bool> EvaluateConditionWithScriptingAsync(
        BpmnProcessState state,
        string condition,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return true;

        try
        {
            // Create a script context with access to process variables
            var scriptContext = new ConditionContext 
            { 
                Variables = state.Variables,
                Condition = condition
            };
            
            // Clean up condition for evaluation
            string scriptCondition = condition.Trim();
            
            // Handle legacy syntax with ${ } by replacing with direct variable access
            if (scriptCondition.Contains("${") && scriptCondition.Contains("}"))
            {
                scriptCondition = Regex.Replace(scriptCondition, @"\$\{([^}]+)\}", match =>
                {
                    var varName = match.Groups[1].Value;
                    return varName;
                });
            }
            
            // Handle special notations for better usability
            scriptCondition = scriptCondition
                .Replace("==", "==") // Keep equals as is
                .Replace("!=", "!=") // Keep not equals as is
                .Replace("&&", "&&") // Keep logical AND as is
                .Replace("||", "||"); // Keep logical OR as is

            // Configure script options with references to commonly needed assemblies
            var scriptOptions = ScriptOptions.Default
                .WithReferences(
                    typeof(System.Linq.Enumerable).Assembly,
                    typeof(System.Collections.Generic.List<>).Assembly,
                    typeof(System.Collections.Generic.Dictionary<,>).Assembly,
                    typeof(ConditionContext).Assembly)
                .WithImports(
                    "System",
                    "System.Linq", 
                    "System.Collections.Generic");
            
            // Execute the script with the context
            var result = await CSharpScript.EvaluateAsync<bool>(
                scriptCondition, 
                scriptOptions, 
                scriptContext, 
                typeof(ConditionContext), 
                cancellationToken);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error evaluating condition script: {Condition}. Defaulting to false.", condition);
            return false;
        }
    }

    private async Task ActivateConditionalOutgoingFlowsAsync(
        BpmnProcessState state,
        ElementCreated @event,
        GatewayInfo gatewayInfo,
        bool activateAllValidPaths,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Evaluating conditional outgoing flows from gateway {GatewayId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        var bpmnDefinition = _definitionStorage.GetParsedDefinition(state.DeploymentKey);
        if (bpmnDefinition == null)
        {
            _logger.LogError("BPMN definition not found for process instance {ProcessInstanceId}",
                @event.ProcessInstanceId);
            return;
        }
        
        var process = FindProcess(bpmnDefinition, state.ProcessDefinitionId);
        if (process == null)
        {
            _logger.LogError("Process definition not found for process instance {ProcessInstanceId}",
                @event.ProcessInstanceId);
            return;
        }

        // Get outgoing flows and evaluate conditions
        var outgoingFlows = FindOutgoingFlows(process, @event.ElementId);
        
        if (!outgoingFlows.Any())
        {
            _logger.LogWarning("No outgoing flows found for gateway {GatewayId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            return;
        }

        // Find the default flow (if any)
        string defaultFlowId = null;
        
        // Try to get default flow from gateway
        if (@event.ElementType == "bpmn:ExclusiveGateway" && process.Items != null)
        {
            var gateway = process.Items.OfType<BpmnExclusiveGateway>()
                .FirstOrDefault(g => g.id == @event.ElementId);
            if (gateway != null)
            {
                defaultFlowId = gateway.@default;
            }
        }
        else if (@event.ElementType == "bpmn:InclusiveGateway" && process.Items != null)
        {
            var gateway = process.Items.OfType<BpmnInclusiveGateway>()
                .FirstOrDefault(g => g.id == @event.ElementId);
            if (gateway != null)
            {
                defaultFlowId = gateway.@default;
            }
        }

        // Evaluate conditions for each flow and collect valid flows (in parallel)
        var validFlows = new List<BpmnSequenceFlow>();
        var invalidFlows = new List<BpmnSequenceFlow>();
        
        var evaluationTasks = new List<Task<(BpmnSequenceFlow Flow, bool IsValid)>>();
        
        foreach (var flow in outgoingFlows)
        {
            // Skip evaluating default flow at this point
            if (flow.id == defaultFlowId)
                continue;
                
            // Start parallel evaluation of each flow's condition
            evaluationTasks.Add(EvaluateFlowConditionAsync(state, flow, cancellationToken));
        }
        
        // Wait for all evaluations to complete
        var evaluationResults = await Task.WhenAll(evaluationTasks);
        
        // Process evaluation results
        foreach (var result in evaluationResults)
        {
            if (result.IsValid)
                validFlows.Add(result.Flow);
            else
                invalidFlows.Add(result.Flow);
        }
        
        // Check if default flow should be taken
        if (validFlows.Count == 0 && !string.IsNullOrEmpty(defaultFlowId))
        {
            var defaultFlow = outgoingFlows.FirstOrDefault(f => f.id == defaultFlowId);
            if (defaultFlow != null)
            {
                validFlows.Add(defaultFlow);
            }
        }
        
        // Only include the default flow if it's needed
        if (!string.IsNullOrEmpty(defaultFlowId) && validFlows.Count == 0)
        {
            var defaultFlow = outgoingFlows.FirstOrDefault(f => f.id == defaultFlowId);
            if (defaultFlow != null)
            {
                validFlows.Add(defaultFlow);
            }
        }
        
        // Store valid and invalid flows in gateway state
        UpdateGatewayFlowState(state, @event.ElementId, @event.ElementType, validFlows, invalidFlows);
        
        // Save state with gateway flow information
        var (_, currentVersion) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId, cancellationToken);
        await _stateStore.SaveStateAsync(@event.ProcessInstanceId, state, currentVersion, cancellationToken);
        
        // Process flows based on gateway type
        if (@event.ElementType == "bpmn:ExclusiveGateway")
        {
            // For XOR, take only first valid flow
            if (validFlows.Any())
            {
                await TransitionToProcessingAsync(state, @event, @event.ExecutionId, cancellationToken);
            }
            else
            {
                // No valid flows, complete immediately
                await _eventBus.PublishAsync(new ElementCompleted
                {
                    ProcessInstanceId = @event.ProcessInstanceId,
                    ElementId = @event.ElementId,
                    ElementType = @event.ElementType,
                    ExecutionId = @event.ExecutionId,
                    IsExecutable = false
                }, cancellationToken);
            }
        }
        else if (@event.ElementType == "bpmn:InclusiveGateway")
        {
            // For OR, take all valid flows
            if (validFlows.Any())
            {
                await TransitionToProcessingAsync(state, @event, @event.ExecutionId, cancellationToken);
            }
            else
            {
                // No valid flows, complete immediately
                await _eventBus.PublishAsync(new ElementCompleted
                {
                    ProcessInstanceId = @event.ProcessInstanceId,
                    ElementId = @event.ElementId,
                    ElementType = @event.ElementType,
                    ExecutionId = @event.ExecutionId,
                    IsExecutable = false
                }, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Evaluates a flow condition asynchronously
    /// </summary>
    private async Task<(BpmnSequenceFlow Flow, bool IsValid)> EvaluateFlowConditionAsync(
        BpmnProcessState state,
        BpmnSequenceFlow flow,
        CancellationToken cancellationToken)
    {
        bool isValid = true;
        
        // If there's a condition, evaluate it
        if (flow.conditionExpression != null)
        {
            try
            {
                // Get the condition expression text
                string condition = null;
                
                // Extract text from the expression
                if (flow.conditionExpression.Text != null && flow.conditionExpression.Text.Length > 0)
                {
                    condition = string.Join("", flow.conditionExpression.Text);
                }
                
                if (!string.IsNullOrEmpty(condition))
                {
                    // Use C# scripting to evaluate the condition
                    isValid = await EvaluateConditionWithScriptingAsync(state, condition, cancellationToken);
                    
                    _logger.LogDebug("Evaluated condition: {Condition} = {Result} for flow {FlowId}",
                        condition, isValid, flow.id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error evaluating condition for flow {FlowId}. Defaulting to false.", flow.id);
                isValid = false;
            }
        }
        
        return (flow, isValid);
    }

    /// <summary>
    /// Updates the gateway state with valid and invalid flows
    /// </summary>
    private void UpdateGatewayFlowState(
        BpmnProcessState state,
        string gatewayId,
        string gatewayType,
        List<BpmnSequenceFlow> validFlows,
        List<BpmnSequenceFlow> invalidFlows)
    {
        // Store the valid and invalid flow IDs in the gateway state for later use
        if (!state.GatewayMergeStates.TryGetValue(gatewayId, out var splitInfo))
        {
            splitInfo = new GatewayMergeInfo
            {
                GatewayId = gatewayId,
                GatewayType = gatewayType,
                ValidOutgoingFlowIds = validFlows.Select(f => f.id).ToList(),
                InvalidOutgoingFlowIds = invalidFlows.Select(f => f.id).ToList()
            };
            state.GatewayMergeStates[gatewayId] = splitInfo;
        }
        else
        {
            splitInfo.ValidOutgoingFlowIds = validFlows.Select(f => f.id).ToList();
            splitInfo.InvalidOutgoingFlowIds = invalidFlows.Select(f => f.id).ToList();
        }
    }
}

/// <summary>
/// Context class for evaluating conditions with C# scripting
/// </summary>
public class ConditionContext
{
    /// <summary>
    /// Process variables accessible to the condition
    /// </summary>
    public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();
    
    /// <summary>
    /// The original condition expression
    /// </summary>
    public string Condition { get; set; }
    
    /// <summary>
    /// Gets a variable from the context
    /// </summary>
    public T GetVariable<T>(string name, T defaultValue = default)
    {
        if (Variables.TryGetValue(name, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }
    
    /// <summary>
    /// Gets a variable value - untyped version
    /// </summary>
    public object GetVariable(string name)
    {
        if (Variables.TryGetValue(name, out var value))
        {
            return value;
        }
        return null;
    }
    
    /// <summary>
    /// Checks if a variable exists in the context
    /// </summary>
    public bool HasVariable(string name)
    {
        return Variables.ContainsKey(name);
    }
    
    /// <summary>
    /// Performs variable comparison, handling different variable types better than == operator
    /// </summary>
    public bool Compare(object left, object right)
    {
        if (left == null && right == null) return true;
        if (left == null || right == null) return false;
        
        // Try converting to common types
        if (left is string || right is string)
        {
            return left.ToString() == right.ToString();
        }
        
        if (left is int leftInt && right is int rightInt)
        {
            return leftInt == rightInt;
        }
        
        if (left is double leftDouble && right is double rightDouble)
        {
            return Math.Abs(leftDouble - rightDouble) < 0.0001;
        }
        
        if (left is bool leftBool && right is bool rightBool)
        {
            return leftBool == rightBool;
        }
        
        // Try numeric conversions
        if (decimal.TryParse(left.ToString(), out var leftDec) &&
            decimal.TryParse(right.ToString(), out var rightDec))
        {
            return leftDec == rightDec;
        }
        
        // Default to standard equality
        return left.Equals(right);
    }
    
    /// <summary>
    /// Implicitly gets a variable value by property access
    /// This allows writing conditions like "amount > 1000" instead of "GetVariable<int>("amount") > 1000"
    /// </summary>
    public dynamic this[string name]
    {
        get
        {
            if (Variables.TryGetValue(name, out var value))
                return value;
            return null;
        }
    }
    
    // Add auto-property accessor for all variables in the dictionary
    // This allows direct access using dot notation in scripts
    // e.g. 'Variables["amount"] > 1000' can be written as 'amount > 1000'
    public dynamic amount => Variables.TryGetValue("amount", out var amount) ? amount : null;
    public dynamic value => Variables.TryGetValue("value", out var value) ? value : null;
    public dynamic name => Variables.TryGetValue("name", out var name) ? name : null;
    public dynamic status => Variables.TryGetValue("status", out var status) ? status : null;
    public dynamic type => Variables.TryGetValue("type", out var type) ? type : null;
    public dynamic id => Variables.TryGetValue("id", out var id) ? id : null;
    public dynamic date => Variables.TryGetValue("date", out var date) ? date : null;
    public dynamic time => Variables.TryGetValue("time", out var time) ? time : null;
    public dynamic count => Variables.TryGetValue("count", out var count) ? count : null;
    public dynamic price => Variables.TryGetValue("price", out var price) ? price : null;
    public dynamic cost => Variables.TryGetValue("cost", out var cost) ? cost : null;
    public dynamic total => Variables.TryGetValue("total", out var total) ? total : null;
    public dynamic approved => Variables.TryGetValue("approved", out var approved) ? approved : null;
    public dynamic rejected => Variables.TryGetValue("rejected", out var rejected) ? rejected : null;
} 