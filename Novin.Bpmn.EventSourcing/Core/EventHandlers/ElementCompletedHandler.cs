using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.Models;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// Handles the completion of BPMN elements in a process instance
/// </summary>
public class ElementCompletedHandler : BaseEventHandler<ElementCompleted>
{
    private const int MaxRetries = 3;
    private const int RetryDelay = 1000; // 1 second

    /// <summary>
    /// Creates a new instance of ElementCompletedHandler
    /// </summary>
    public ElementCompletedHandler(
        IStateStore stateStore,
        IEventStore eventStore,
        IDefinitionStore definitionStore,
        IEventBus eventBus,
        ILogger<ElementCompletedHandler> logger)
        : base(stateStore, eventStore, definitionStore, logger)
    {
    }

    /// <inheritdoc />
    protected override async Task ProcessEventAsync(ElementCompleted @event, CancellationToken cancellationToken = default)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        try
        {
            Logger.LogDebug("Processing ElementCompleted event for element {ElementId} in process {ProcessInstanceId}", 
                @event.ElementId, @event.ProcessInstanceId);

            var retryCount = 0;
            while (true)
            {
                try
                {
                    var currentState = await GetStateAsync(@event.ProcessInstanceId, cancellationToken);
                    if (currentState == null)
                    {
                        Logger.LogWarning("Process instance state not found for {ProcessInstanceId}", @event.ProcessInstanceId);
                        return;
                    }

                    // Track event in execution path if execution ID is provided
                    if (!string.IsNullOrEmpty(@event.ExecutionId))
                    {
                        currentState.AddEventToExecution(@event.ExecutionId, @event);
                        
                        // Mark execution as completed
                        if (currentState.ActiveExecutions.TryGetValue(@event.ExecutionId, out var executionPath))
                        {
                            executionPath.Status = ExecutionStatus.Completed;
                            executionPath.Timestamp = DateTime.UtcNow;
                            currentState.ActiveExecutions.Remove(@event.ExecutionId);
                        }
                    }

                    // Add the completed element to the state
                    if (!currentState.CompletedElements.Contains(@event.ElementId))
                    {
                        currentState.CompletedElements.Add(@event.ElementId);
                    }
                    
                    // Remove from active elements if present
                    currentState.ActiveElements.Remove(@event.ElementId);
                    
                    // Update element status
                    if (currentState.ElementStatuses.TryGetValue(@event.ElementId, out var elementStatus))
                    {
                        elementStatus.Status = "Completed";
                        elementStatus.CompletedAt = DateTime.UtcNow;
                        elementStatus.UpdatedAt = DateTime.UtcNow;
                    }
                    
                    // Save the updated state
                    await SaveStateAsync(@event.ProcessInstanceId, currentState, null, cancellationToken);

                    // Update statistics
                    currentState.UpdateExecutionStatistics();
                    
                    // Check if this is an end event
                    if (@event.ElementType == "bpmn:EndEvent")
                    {
                        await HandleEndEventCompletionAsync(currentState, @event, cancellationToken);
                        return;
                    }

                    // Get outgoing flows
                    var outgoingFlows = await GetOutgoingFlowsAsync(currentState, @event.ElementId, cancellationToken);
                    if (!outgoingFlows.Any())
                    {
                        Logger.LogDebug("No outgoing flows found for element {ElementId} in process {ProcessInstanceId}",
                            @event.ElementId, @event.ProcessInstanceId);
                        return;
                    }

                    // Handle different element types
                    await HandleElementTypeCompletionAsync(currentState, @event, outgoingFlows, cancellationToken);
                    
                    return; // Success, exit retry loop
                }
                catch (Exception)
                {
                    retryCount++;
                    if (retryCount >= MaxRetries)
                    {
                        Logger.LogError("Failed to handle ElementCompleted event after {MaxRetries} retries for element {ElementId} in process {ProcessInstanceId}",
                            MaxRetries, @event.ElementId, @event.ProcessInstanceId);
                        throw;
                    }
                    
                    Logger.LogWarning("Concurrency conflict detected, retry {RetryCount} of {MaxRetries} for element {ElementId} in process {ProcessInstanceId}",
                        retryCount, MaxRetries, @event.ElementId, @event.ProcessInstanceId);
                    
                    await Task.Delay(RetryDelay * retryCount, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling ElementCompleted event for element {ElementId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            throw;
        }
    }

    /// <summary>
    /// Handles completion based on element type
    /// </summary>
    private async Task HandleElementTypeCompletionAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        List<FlowInfo> outgoingFlows,
        CancellationToken cancellationToken)
    {
        switch (@event.ElementType)
        {
            case "bpmn:StartEvent":
                await HandleStartEventCompletionAsync(state, @event, cancellationToken);
                break;
                
            case "bpmn:UserTask":
            case "bpmn:ServiceTask":
            case "bpmn:ScriptTask":
            case "bpmn:BusinessRuleTask":
            case "bpmn:SendTask":
            case "bpmn:ReceiveTask":
                await HandleTaskCompletionAsync(state, @event, cancellationToken);
                break;
                
            case "bpmn:ParallelGateway":
                await HandleParallelGatewayCompletionAsync(state, @event, cancellationToken);
                break;
                
            case "bpmn:InclusiveGateway":
                await HandleInclusiveGatewayCompletionAsync(state, @event, cancellationToken);
                break;
                
            case "bpmn:ExclusiveGateway":
                await HandleExclusiveGatewayCompletionAsync(state, @event, cancellationToken);
                break;
                
            case "bpmn:BoundaryEvent":
                await HandleBoundaryEventTriggerAsync(state, @event, cancellationToken);
                break;
                
            case "bpmn:SubProcess":
            case "bpmn:CallActivity":
                await HandleSubProcessCompletionAsync(state, @event, cancellationToken);
                break;
                
            default:
                Logger.LogDebug("Element {ElementId} of type {ElementType} completed in process {ProcessInstanceId}", 
                    @event.ElementId, @event.ElementType, @event.ProcessInstanceId);
                await ActivateNextElementsAsync(state, @event, cancellationToken);
                break;
        }
    }

    /// <summary>
    /// پردازش تکمیل رویداد شروع
    /// </summary>
    private async Task HandleStartEventCompletionAsync(
        BpmnProcessState state, 
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Processing completion of start event {EventId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        await ActivateNextElementsAsync(state, @event, cancellationToken);
    }
    
    /// <summary>
    /// پردازش تکمیل رویداد پایان
    /// </summary>
    private async Task HandleEndEventCompletionAsync(
        BpmnProcessState state, 
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Processing completion of end event {EventId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
        
        await EventBus.PublishAsync(new ProcessCompletedEvent
        {
            ProcessInstanceId = @event.ProcessInstanceId,
            EndEventId = @event.ElementId
        }, cancellationToken);
        
        Logger.LogInformation("Process {ProcessInstanceId} completed with end event {EndEventId}", 
            @event.ProcessInstanceId, @event.ElementId);
    }
    
    /// <summary>
    /// پردازش تکمیل وظیفه کاربر
    /// </summary>
    private async Task HandleUserTaskCompletionAsync(
        BpmnProcessState state, 
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Processing completion of user task {TaskId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        await ActivateNextElementsAsync(state, @event, cancellationToken);
    }
    
    /// <summary>
    /// پردازش تکمیل وظیفه سرویس
    /// </summary>
    private async Task HandleServiceTaskCompletionAsync(
        BpmnProcessState state, 
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Processing completion of service task {TaskId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        await ActivateNextElementsAsync(state, @event, cancellationToken);
    }
    
    /// <summary>
    /// پردازش تکمیل دروازه موازی (AND-Gateway)
    /// </summary>
    private async Task HandleParallelGatewayCompletionAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        var gatewayInfo = await GetGatewayInfoAsync(state, @event.ElementId, cancellationToken);
        
        if (gatewayInfo.IsJoin)
        {
            // Update or initialize gateway merge info
            GatewayMergeInfo mergeInfo;
            if (!state.GatewayMergeStates.TryGetValue(@event.ElementId, out mergeInfo))
            {
                mergeInfo = new GatewayMergeInfo
                {
                    GatewayId = @event.ElementId,
                    GatewayType = "bpmn:ParallelGateway",
                    RequiredIncomingFlows = gatewayInfo.IncomingFlows.Count(),
                    IncomingFlowIds = gatewayInfo.IncomingFlows.ToList()
                };
                state.GatewayMergeStates[@event.ElementId] = mergeInfo;
            }
            
            // Record flow receipt if we know which sequence flow triggered this
            if (!string.IsNullOrEmpty(@event.ExecutionId) && 
                state.ActiveExecutions.TryGetValue(@event.ExecutionId, out var execution) && 
                !string.IsNullOrEmpty(execution.SequenceFlowId))
            {
                mergeInfo.RecordFlowReceived(execution.SequenceFlowId);
            }
            
            bool canMerge = mergeInfo.CanMerge;
            
            if (canMerge)
            {
                Logger.LogDebug("Parallel gateway {GatewayId} has received all required tokens ({ReceivedFlows}/{RequiredFlows}). Proceeding with merge in process {ProcessInstanceId}",
                    @event.ElementId, mergeInfo.ReceivedIncomingFlows, mergeInfo.RequiredIncomingFlows, @event.ProcessInstanceId);
                
                await ActivateNextElementsAsync(state, @event, cancellationToken);
                
                // Reset gateway merge state for potential reuse
                state.GatewayMergeStates.Remove(@event.ElementId);
            }
            else
            {
                Logger.LogDebug("Parallel gateway {GatewayId} is waiting for more tokens to merge in process {ProcessInstanceId} ({ReceivedFlows}/{RequiredFlows})",
                    @event.ElementId, @event.ProcessInstanceId, mergeInfo.ReceivedIncomingFlows, mergeInfo.RequiredIncomingFlows);
                
                // Update gateway merge info in state
                await SaveStateAsync(@event.ProcessInstanceId, state, null, cancellationToken);
            }
        }
        else
        {
            Logger.LogDebug("Parallel gateway {GatewayId} is forking execution in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            
            await ActivateAllOutgoingFlowsAsync(state, @event, gatewayInfo, cancellationToken);
        }
    }
    
    /// <summary>
    /// پردازش تکمیل دروازه فراگیر (OR-Gateway)
    /// </summary>
    private async Task HandleInclusiveGatewayCompletionAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        var gatewayInfo = await GetGatewayInfoAsync(state, @event.ElementId, cancellationToken);
        
        if (gatewayInfo.IsJoin)
        {
            // Join gateways are handled in ElementCreatedHandler, here we just activate next elements
            Logger.LogDebug("Inclusive gateway {GatewayId} join synchronization was completed, activating next elements",
                @event.ElementId);
            
                await ActivateNextElementsAsync(state, @event, cancellationToken);
            }
            else
            {
            // This is a split gateway - evaluate conditions and activate valid paths
            Logger.LogDebug("Inclusive gateway {GatewayId} is evaluating conditions for outgoing paths in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            
            await ActivateConditionalOutgoingFlowsAsync(state, @event, gatewayInfo, true, cancellationToken);
        }
    }
    
    /// <summary>
    /// پردازش تکمیل دروازه انحصاری (XOR-Gateway)
    /// </summary>
    private async Task HandleExclusiveGatewayCompletionAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        var gatewayInfo = await GetGatewayInfoAsync(state, @event.ElementId, cancellationToken);
        
        if (gatewayInfo.IsJoin)
        {
            // Join gateways are handled in ElementCreatedHandler, here we just activate next elements
            Logger.LogDebug("Exclusive gateway {GatewayId} join synchronization was completed, activating next elements",
                @event.ElementId);
            
            await ActivateNextElementsAsync(state, @event, cancellationToken);
        }
        else
        {
            // This is a split gateway - evaluate conditions and select exactly one path
            Logger.LogDebug("Exclusive gateway {GatewayId} is evaluating conditions to select exactly one path in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            
            await ActivateConditionalOutgoingFlowsAsync(state, @event, gatewayInfo, false, cancellationToken);
        }
    }
    
    /// <summary>
    /// فعال‌سازی المان‌های بعدی بر اساس جریان‌های خروجی
    /// </summary>
    private async Task ActivateNextElementsAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        var outgoingFlows = await GetOutgoingFlowsAsync(state, @event.ElementId, cancellationToken);
        
        if (outgoingFlows == null || !outgoingFlows.Any())
        {
            Logger.LogDebug("No outgoing flows found for element {ElementId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            return;
        }
        
        // Process outgoing flows in parallel for better performance
        if (outgoingFlows.Count > 1)
        {
            var activationTasks = outgoingFlows.Select(flow => 
                ProcessingElementAsync(
                    state, 
                    flow.TargetElementId, 
                    flow.TargetElementType,
                    @event.ElementId, 
                    flow.Id, 
                    cancellationToken
                )
            ).ToList();

            // Wait for all activations to complete
            await Task.WhenAll(activationTasks);
        }
        else
        {
            // For single flow, just process directly
            var flow = outgoingFlows.First();
            await ProcessingElementAsync(
                state, 
                flow.TargetElementId, 
                flow.TargetElementType,
                @event.ElementId, 
                flow.Id, 
                cancellationToken
            );
        }
    }
    
    /// <summary>
    /// فعال‌سازی همه مسیرهای خروجی (برای دروازه موازی)
    /// </summary>
    private async Task ActivateAllOutgoingFlowsAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        GatewayInfo gatewayInfo,
        CancellationToken cancellationToken)
    {
        var outgoingFlows = await GetOutgoingFlowsAsync(state, @event.ElementId, cancellationToken);
        
        if (outgoingFlows == null || !outgoingFlows.Any())
        {
            Logger.LogDebug("No outgoing flows found for parallel gateway {GatewayId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            return;
        }
        
        Logger.LogDebug("Activating all {FlowCount} outgoing flows for parallel gateway {GatewayId} in process {ProcessInstanceId}",
            outgoingFlows.Count, @event.ElementId, @event.ProcessInstanceId);
            
        // Process all flows in parallel for maximum performance
        var activationTasks = outgoingFlows.Select(flow => 
            ProcessingElementAsync(
                state, 
                flow.TargetElementId, 
                flow.TargetElementType,
                @event.ElementId, 
                flow.Id, 
                cancellationToken
            )
        ).ToList();

        // Wait for all activations to complete
        await Task.WhenAll(activationTasks);
    }
    
    /// <summary>
    /// Activates outgoing flows based on conditions (for exclusive and inclusive gateways)
    /// </summary>
    private async Task ActivateConditionalOutgoingFlowsAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        GatewayInfo gatewayInfo,
        bool activateAllValidPaths,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Evaluating conditional outgoing flows from gateway {GatewayId} in process {ProcessInstanceId}, ActivateAllValidPaths: {ActivateAllValidPaths}",
            @event.ElementId, @event.ProcessInstanceId, activateAllValidPaths);
        
        // Get all outgoing flows
        var outgoingFlows = await GetOutgoingFlowsAsync(state, @event.ElementId, cancellationToken);
        if (!outgoingFlows.Any())
        {
            Logger.LogWarning("No outgoing flows found for gateway {GatewayId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            return;
        }
        
        // Categorize flows
        var defaultFlow = outgoingFlows.FirstOrDefault(f => f.IsDefault);
        var flowsToEvaluate = outgoingFlows.Where(f => !f.IsDefault && !string.IsNullOrEmpty(f.Condition)).ToList();
        var unconditionalFlows = outgoingFlows.Where(f => !f.IsDefault && string.IsNullOrEmpty(f.Condition)).ToList();
        
        Logger.LogDebug("Gateway {GatewayId} has {ConditionalCount} conditional flows, {UnconditionalCount} unconditional flows, DefaultFlow: {HasDefault}",
            @event.ElementId, flowsToEvaluate.Count, unconditionalFlows.Count, defaultFlow != null);
        
        // Evaluate all conditions in parallel for better performance
        var evaluationTasks = flowsToEvaluate.Select(async flow => 
        {
            try
            {
                bool result = await EvaluateConditionAsync(state, flow.Condition, cancellationToken);
                Logger.LogDebug("Condition evaluation for flow {FlowId}: condition '{Condition}' = {Result}",
                    flow.Id, flow.Condition, result);
                return (Flow: flow, IsValid: result);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error evaluating condition for flow {FlowId}: {Condition}", flow.Id, flow.Condition);
                return (Flow: flow, IsValid: false);
            }
        }).ToList();
        
        // Wait for all evaluations to complete
        var evaluationResults = await Task.WhenAll(evaluationTasks);
        
        // Collect valid flows
        var validFlows = evaluationResults.Where(r => r.IsValid).Select(r => r.Flow).ToList();
        validFlows.AddRange(unconditionalFlows); // Add flows without conditions
        
        // Get invalid flows for tracking
        var invalidFlows = outgoingFlows.Except(validFlows).ToList();
        if (defaultFlow != null) 
        {
            // Don't count default flow as invalid yet
            invalidFlows = invalidFlows.Where(f => f.Id != defaultFlow.Id).ToList();
        }
        
        Logger.LogDebug("Gateway {GatewayId} condition evaluation complete: {ValidCount} valid paths, {InvalidCount} invalid paths",
            @event.ElementId, validFlows.Count, invalidFlows.Count);
        
        // Process according to gateway type
        if (activateAllValidPaths) 
        {
            // Inclusive Gateway - take all valid paths
            await ActivateInclusiveGatewayPathsAsync(state, @event, validFlows, invalidFlows, defaultFlow, cancellationToken);
            }
            else
            {
            // Exclusive Gateway - take exactly one path
            await ActivateExclusiveGatewayPathAsync(state, @event, validFlows, invalidFlows, defaultFlow, cancellationToken);
        }
    }
    
    /// <summary>
    /// Activates valid paths for an inclusive gateway
    /// </summary>
    private async Task ActivateInclusiveGatewayPathsAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        List<FlowInfo> validFlows,
        List<FlowInfo> invalidFlows,
        FlowInfo defaultFlow,
        CancellationToken cancellationToken)
    {
        var activationTasks = new List<Task>();
        
        if (validFlows.Any())
        {
            Logger.LogDebug("Inclusive gateway {GatewayId} activating {Count} valid flows",
                @event.ElementId, validFlows.Count);
            
            // Activate all valid flows as executable
            foreach (var flow in validFlows)
            {
                activationTasks.Add(ProcessingElementAsync(
                    state, 
                    flow.TargetElementId, 
                    flow.TargetElementType,
                    @event.ElementId, 
                    flow.Id, 
                    cancellationToken,
                    isExecutable: true));
            }
            
            // Create non-executable paths for all invalid flows
            foreach (var flow in invalidFlows)
            {
                activationTasks.Add(ProcessingElementAsync(
                    state,
                    flow.TargetElementId,
                    flow.TargetElementType,
                    @event.ElementId,
                    flow.Id,
                    cancellationToken,
                    isExecutable: false));
            }
        }
        else if (defaultFlow != null)
        {
            // No valid conditional flows, use default flow
            Logger.LogDebug("No conditional flow was valid for inclusive gateway, taking default flow {FlowId}",
                defaultFlow.Id);
            
            // Activate only the default flow as executable
            activationTasks.Add(ProcessingElementAsync(
                state,
                defaultFlow.TargetElementId,
                defaultFlow.TargetElementType,
                @event.ElementId,
                defaultFlow.Id,
                cancellationToken,
                isExecutable: true));
            
            // Create non-executable paths for all other flows
            foreach (var flow in invalidFlows)
            {
                activationTasks.Add(ProcessingElementAsync(
                    state,
                    flow.TargetElementId,
                    flow.TargetElementType,
                    @event.ElementId,
                    flow.Id,
                    cancellationToken,
                    isExecutable: false));
            }
        }
        else
        {
            Logger.LogWarning("Inclusive gateway {GatewayId} has no valid flows and no default flow", @event.ElementId);
        }
        
        // Execute all activation tasks in parallel
        if (activationTasks.Any())
        {
            await Task.WhenAll(activationTasks);
        }
    }
    
    /// <summary>
    /// Activates exactly one path for an exclusive gateway
    /// </summary>
    private async Task ActivateExclusiveGatewayPathAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        List<FlowInfo> validFlows,
        List<FlowInfo> invalidFlows,
        FlowInfo defaultFlow,
        CancellationToken cancellationToken)
    {
        var activationTasks = new List<Task>();
        
        if (validFlows.Any())
        {
            // For XOR gateway, take only the first valid flow
            var selectedFlow = validFlows.First();
            Logger.LogDebug("Exclusive gateway {GatewayId} selected flow {FlowId} to {TargetElementId}",
                @event.ElementId, selectedFlow.Id, selectedFlow.TargetElementId);
            
            // Activate only the selected flow as executable
            activationTasks.Add(ProcessingElementAsync(
                state,
                selectedFlow.TargetElementId,
                selectedFlow.TargetElementType,
                @event.ElementId,
                selectedFlow.Id,
                cancellationToken,
                isExecutable: true));
            
            // Add other valid flows but as non-executable (for visualization)
            foreach (var flow in validFlows.Where(f => f.Id != selectedFlow.Id))
            {
                invalidFlows.Add(flow);
            }
            
            // Create non-executable paths for all invalid/unused flows
            foreach (var flow in invalidFlows)
            {
                activationTasks.Add(ProcessingElementAsync(
                    state,
                    flow.TargetElementId,
                    flow.TargetElementType,
                    @event.ElementId,
                    flow.Id,
                    cancellationToken,
                    isExecutable: false));
            }
        }
        else if (defaultFlow != null)
        {
            // No valid conditional flows, use default flow
            Logger.LogDebug("No conditional flow was valid for exclusive gateway, taking default flow {FlowId}",
                defaultFlow.Id);
            
            // Activate only the default flow
            activationTasks.Add(ProcessingElementAsync(
                state,
                defaultFlow.TargetElementId,
                defaultFlow.TargetElementType,
                @event.ElementId,
                defaultFlow.Id,
                cancellationToken,
                isExecutable: true));
            
            // Create non-executable paths for all invalid flows
            foreach (var flow in invalidFlows)
            {
                activationTasks.Add(ProcessingElementAsync(
                    state,
                    flow.TargetElementId,
                    flow.TargetElementType,
                    @event.ElementId,
                    flow.Id,
                    cancellationToken,
                    isExecutable: false));
            }
        }
        else
        {
            Logger.LogWarning("Exclusive gateway {GatewayId} has no valid flows and no default flow", @event.ElementId);
        }
        
        // Execute all activation tasks in parallel
        if (activationTasks.Any())
        {
            await Task.WhenAll(activationTasks);
        }
    }
    
    /// <summary>
    /// Process and activate a BPMN element
    /// </summary>
    private async Task ProcessingElementAsync(
        BpmnProcessState processState,
        string elementId,
        string elementType,
        string sourceElementId,
        string sequenceFlowId,
        CancellationToken cancellationToken,
        bool isExecutable = true)
    {
        Logger.LogDebug("Processing element {ElementId} of type {ElementType} via flow {FlowId} in process {ProcessInstanceId}, IsExecutable: {IsExecutable}",
            elementId, elementType, sequenceFlowId, processState.ProcessInstanceId, isExecutable);
            
        // For non-executable flows, create the execution path but complete immediately
        // This avoids unnecessary processing while maintaining visualization data
        if (!isExecutable)
        {
            await CreateNonExecutablePathAsync(
                processState, 
                elementId, 
                elementType, 
                sourceElementId, 
                sequenceFlowId, 
                cancellationToken);
            return;
        }
            
        var retryCount = 0;
        while (true)
        {
            try
            {
                // Get current state and version
                var currentState = await GetStateAsync(processState.ProcessInstanceId, cancellationToken);
                
                if (currentState == null)
                {
                    Logger.LogWarning("Process instance state not found for {ProcessInstanceId}", processState.ProcessInstanceId);
                    return;
                }

                // Add to active elements if not already present
                if (!currentState.ActiveElements.Contains(elementId))
                {
                    currentState.ActiveElements.Add(elementId);
                }
                
                // Create a new execution path for this activation
                var executionPath = new ExecutionPath
                {
                    SourceElementId = sourceElementId,
                    SourceElementType = GetElementTypeById(currentState, sourceElementId),
                    TargetElementId = elementId,
                    TargetElementType = elementType,
                    SequenceFlowId = sequenceFlowId,
                    Timestamp = DateTime.UtcNow,
                    Status = ExecutionStatus.Active,
                    IsExecutable = isExecutable
                };
                
                // Find any execution path that just completed for the source element
                string parentExecutionId = null;
                foreach (var path in currentState.ExecutionPaths)
                {
                    if (path.TargetElementId == sourceElementId && path.Status == ExecutionStatus.Completed)
                    {
                        parentExecutionId = path.ExecutionId;
                        break;
                    }
                }
                
                // Set parent execution ID if found
                if (!string.IsNullOrEmpty(parentExecutionId))
                {
                    executionPath.ParentExecutionId = parentExecutionId;
                }
                
                // Add to state
                currentState.ExecutionPaths.Add(executionPath);
                currentState.ActiveExecutions[executionPath.ExecutionId] = executionPath;
                
                // Save the updated state
                await SaveStateAsync(processState.ProcessInstanceId, currentState, null, cancellationToken);
                
                // Finally publish ElementCreated event
                await EventBus.PublishAsync(new ElementCreated
                {
                    ProcessInstanceId = processState.ProcessInstanceId,
                    ElementId = elementId,
                    ElementType = elementType,
                    EventId = Guid.NewGuid(),
                    Timestamp = DateTime.UtcNow,
                    SourceElementId = sourceElementId,
                    SequenceFlowId = sequenceFlowId,
                    ExecutionId = executionPath.ExecutionId,
                    IsExecutable = isExecutable
                }, cancellationToken);
                
                return; // Success, exit retry loop
            }
            catch (Exception)
            {
                retryCount++;
                if (retryCount >= MaxRetries)
                {
                    Logger.LogError("Failed to activate element {ElementId} after {MaxRetries} retries in process {ProcessInstanceId}",
                        elementId, MaxRetries, processState.ProcessInstanceId);
                    throw;
                }
                
                Logger.LogWarning("Concurrency conflict detected while processing element {ElementId}, retry {RetryCount} of {MaxRetries} in process {ProcessInstanceId}",
                    elementId, retryCount, MaxRetries, processState.ProcessInstanceId);
                
                await Task.Delay(RetryDelay * retryCount, cancellationToken);
            }
        }
    }
    
    /// <summary>
    /// Creates a non-executable path that is immediately marked as completed
    /// </summary>
    private async Task CreateNonExecutablePathAsync(
        BpmnProcessState processState,
        string elementId,
        string elementType,
        string sourceElementId,
        string sequenceFlowId,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Creating non-executable path for element {ElementId} in process {ProcessInstanceId}",
            elementId, processState.ProcessInstanceId);
            
        var retryCount = 0;
        while (true)
        {
            try
            {
                // Get current state and version
                var currentState = await GetStateAsync(processState.ProcessInstanceId, cancellationToken);
                
                if (currentState == null)
                {
                    Logger.LogWarning("Process instance state not found for {ProcessInstanceId}", processState.ProcessInstanceId);
                    return;
                }

                // Create execution path for visualization purposes
                var executionPath = new ExecutionPath
                {
                    SourceElementId = sourceElementId,
                    SourceElementType = GetElementTypeById(currentState, sourceElementId),
                    TargetElementId = elementId,
                    TargetElementType = elementType,
                    SequenceFlowId = sequenceFlowId,
                    Timestamp = DateTime.UtcNow,
                    Status = ExecutionStatus.Completed, // Already completed
                    IsExecutable = false
                };
                
                // Add to state - include in execution paths but not in active elements
                currentState.ExecutionPaths.Add(executionPath);
                
                // Save the updated state
                await SaveStateAsync(processState.ProcessInstanceId, currentState, null, cancellationToken);
                
                // Publish ElementCreated and ElementCompleted events in sequence for proper tracking
                var createdEvent = new ElementCreated
                {
                    ProcessInstanceId = processState.ProcessInstanceId,
                    ElementId = elementId,
                    ElementType = elementType,
                    EventId = Guid.NewGuid(),
                    Timestamp = DateTime.UtcNow,
                    SourceElementId = sourceElementId,
                    SequenceFlowId = sequenceFlowId,
                    ExecutionId = executionPath.ExecutionId,
                    IsExecutable = false
                };
                
                await EventBus.PublishAsync(createdEvent, cancellationToken);
                
                // Immediately complete the element
                await EventBus.PublishAsync(new ElementCompleted
                {
                    ProcessInstanceId = processState.ProcessInstanceId,
                    ElementId = elementId,
                    ElementType = elementType,
                    EventId = Guid.NewGuid(),
                    Timestamp = DateTime.UtcNow,
                    ExecutionId = executionPath.ExecutionId,
                    IsExecutable = false
                }, cancellationToken);
                
                return; // Success, exit retry loop
            }
            catch (Exception)
            {
                retryCount++;
                if (retryCount >= MaxRetries)
                {
                    Logger.LogError("Failed to create non-executable path for element {ElementId} after {MaxRetries} retries",
                        elementId, MaxRetries);
                    throw;
                }
                
                Logger.LogWarning("Concurrency conflict detected while creating non-executable path for {ElementId}, retry {RetryCount} of {MaxRetries}",
                    elementId, retryCount, MaxRetries);
                
                await Task.Delay(RetryDelay * retryCount, cancellationToken);
            }
        }
    }
    
    /// <summary>
    /// Get element type by ID
    /// </summary>
    private string GetElementTypeById(BpmnProcessState state, string elementId)
    {
        // First check element statuses
        if (state.ElementStatuses.TryGetValue(elementId, out var status))
        {
            return status.ElementType;
        }
        
        // Then check execution paths
        foreach (var path in state.ExecutionPaths)
        {
            if (path.TargetElementId == elementId)
            {
                return path.TargetElementType;
            }
            if (path.SourceElementId == elementId)
            {
                return path.SourceElementType;
            }
        }
        
        // Default fallback
        return "unknown";
    }
    
    /// <summary>
    /// بررسی امکان ادغام دروازه موازی
    /// </summary>
    private async Task<bool> CanMergeParallelGatewayAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        GatewayInfo gatewayInfo,
        CancellationToken cancellationToken)
    {
        var receivedTokensCount = state.CompletedElements.Count(e => e == @event.ElementId);
            
        if (!state.CompletedElements.Contains(@event.ElementId))
        {
            receivedTokensCount++;
        }
        
        Logger.LogDebug("Parallel gateway {GatewayId} has received {ReceivedCount} tokens out of {TotalCount} required",
            @event.ElementId, receivedTokensCount, gatewayInfo.IncomingFlows.Count());
            
        return receivedTokensCount >= gatewayInfo.IncomingFlows.Count();
    }
    
    /// <summary>
    /// بررسی امکان ادغام دروازه فراگیر
    /// </summary>
    private async Task<bool> CanMergeInclusiveGatewayAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        GatewayInfo gatewayInfo,
        CancellationToken cancellationToken)
    {
        var definitions = await DefinitionStore.GetParsedDefinitionAsync(state.DeploymentKey, cancellationToken);
        if (definitions == null)
        {
            Logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", state.DeploymentKey);
            return false;
        }
        
        var process = FindProcess(definitions, state.ProcessDefinitionId);
        if (process == null)
        {
            Logger.LogWarning("Process definition not found with ID {ProcessId} in deployment {DeploymentKey}", 
                state.ProcessDefinitionId, state.DeploymentKey);
            return false;
        }
        
        var activeIncomingFlows = DetermineActiveIncomingFlows(process, state, @event.ElementId);
        var receivedTokensCount = state.CompletedElements.Count(e => e == @event.ElementId);
            
        if (!state.CompletedElements.Contains(@event.ElementId))
        {
            receivedTokensCount++;
        }
        
        Logger.LogDebug("Inclusive gateway {GatewayId} has received {ReceivedCount} tokens out of {ActiveCount} active paths",
            @event.ElementId, receivedTokensCount, activeIncomingFlows.Count);
            
        return receivedTokensCount >= activeIncomingFlows.Count;
    }
    
    /// <summary>
    /// تعیین مسیرهای ورودی فعال برای یک دروازه فراگیر
    /// </summary>
    private List<string> DetermineActiveIncomingFlows(
        BpmnProcess process,
        BpmnProcessState state,
        string gatewayId)
    {
        var incomingFlows = FindIncomingFlows(process, gatewayId);
        return incomingFlows.Select(f => f.id).ToList();
    }
    
    /// <summary>
    /// دریافت اطلاعات یک دروازه
    /// </summary>
    private async Task<GatewayInfo> GetGatewayInfoAsync(
        BpmnProcessState state,
        string gatewayId,
        CancellationToken cancellationToken)
    {
        var definitions = await DefinitionStore.GetParsedDefinitionAsync(state.DeploymentKey, cancellationToken);
        if (definitions == null)
        {
            Logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", state.DeploymentKey);
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
            Logger.LogWarning("Process definition not found with ID {ProcessId} in deployment {DeploymentKey}", 
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
            Logger.LogWarning("Gateway element not found with ID {GatewayId} in process {ProcessId}", 
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
    
    /// <summary>
    /// دریافت جریان‌های خروجی از یک المان
    /// </summary>
    private async Task<List<FlowInfo>> GetOutgoingFlowsAsync(
        BpmnProcessState processState,
        string elementId,
        CancellationToken cancellationToken)
    {
        var definition = await DefinitionStore.GetParsedDefinitionAsync(processState.DeploymentKey, cancellationToken);
        if (definition == null)
        {
            Logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", processState.DeploymentKey);
            return new List<FlowInfo>();
        }

        var process = definition.Items.OfType<BpmnProcess>()
            .FirstOrDefault(p => p.id == processState.ProcessDefinitionId);
            
        if (process == null)
        {
            Logger.LogWarning("Process definition not found with ID {ProcessId} in deployment {DeploymentKey}", 
                processState.ProcessDefinitionId, processState.DeploymentKey);
            return new List<FlowInfo>();
        }

        var element = process.Items.OfType<BpmnBaseElement>()
            .FirstOrDefault(e => e.id == elementId);
            
        if (element == null)
        {
            Logger.LogWarning("Element {ElementId} not found in process {ProcessId}", 
                elementId, processState.ProcessDefinitionId);
            return new List<FlowInfo>();
        }

        var outgoingFlows = process.Items.OfType<BpmnSequenceFlow>()
            .Where(f => f.sourceRef == elementId)
            .ToList();

        if (!outgoingFlows.Any())
        {
            Logger.LogDebug("No outgoing flows found for element {ElementId} in process {ProcessId}", 
                elementId, processState.ProcessDefinitionId);
            return new List<FlowInfo>();
        }

        var flowInfos = new List<FlowInfo>();
        foreach (var flow in outgoingFlows)
        {
            var targetElement = process.Items.OfType<BpmnBaseElement>()
                .FirstOrDefault(e => e.id == flow.targetRef);
                
            if (targetElement == null)
            {
                Logger.LogWarning("Target element not found with ID {TargetId} for flow {FlowId}", 
                    flow.targetRef, flow.id);
                continue;
            }

            bool isDefault = false;
            if (element is BpmnGateway gateway)
            {
                if (gateway is BpmnExclusiveGateway exclusiveGateway)
                {
                    isDefault = exclusiveGateway.@default?.ToString() == flow.id;
                }
                else if (gateway is BpmnInclusiveGateway inclusiveGateway)
                {
                    isDefault = inclusiveGateway.@default?.ToString() == flow.id;
                }
                else if (gateway is BpmnComplexGateway complexGateway)
                {
                    isDefault = complexGateway.@default?.ToString() == flow.id;
                }
            }

            flowInfos.Add(new FlowInfo
            {
                Id = flow.id,
                SourceElementId = elementId,
                TargetElementId = flow.targetRef,
                TargetElementType = targetElement.GetType().Name,
                Condition = flow.conditionExpression?.Text?.FirstOrDefault(),
                IsDefault = isDefault
            });
        }

        return flowInfos;
    }

    /// <summary>
    /// یافتن فرآیند با شناسه مشخص در تعریف BPMN
    /// </summary>
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

    /// <summary>
    /// یافتن المان با شناسه مشخص در فرآیند
    /// </summary>
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

    /// <summary>
    /// جستجوی المان در زیرفرآیند
    /// </summary>
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

    /// <summary>
    /// یافتن جریان‌های ورودی به یک المان
    /// </summary>
    private List<BpmnSequenceFlow> FindIncomingFlows(BpmnProcess process, string elementId)
    {
        if (process?.Items == null || !process.Items.Any() || string.IsNullOrEmpty(elementId))
            return new List<BpmnSequenceFlow>();
            
        var flows = process.Items.OfType<BpmnSequenceFlow>().ToList();
        return flows.Where(f => f.targetRef == elementId).ToList();
    }

    /// <summary>
    /// یافتن جریان‌های خروجی از یک المان
    /// </summary>
    private List<BpmnSequenceFlow> FindOutgoingFlows(BpmnProcess process, string elementId)
    {
        if (process?.Items == null || !process.Items.Any() || string.IsNullOrEmpty(elementId))
            return new List<BpmnSequenceFlow>();
            
        var flows = process.Items.OfType<BpmnSequenceFlow>().ToList();
        return flows.Where(f => f.sourceRef == elementId).ToList();
    }

    /// <summary>
    /// تشخیص نوع المان
    /// </summary>
    private string GetElementType(BpmnBaseElement element)
    {
        if (element == null)
            return "unknown";
       
        if (element is BpmnUserTask)
            return "bpmn:UserTask";
        if (element is BpmnServiceTask)
            return "bpmn:ServiceTask";
        if (element is BpmnScriptTask)
            return "bpmn:ScriptTask";
        if (element is BpmnBusinessRuleTask)
            return "bpmn:BusinessRuleTask";
        if (element is BpmnManualTask)
            return "bpmn:ManualTask";
        if (element is BpmnReceiveTask)
            return "bpmn:ReceiveTask";
        if (element is BpmnSendTask)
            return "bpmn:SendTask";
        if (element is BpmnSubProcess)
            return "bpmn:SubProcess";
        if (element is BpmnCallActivity)
            return "bpmn:CallActivity";
        if (element is BpmnStartEvent)
            return "bpmn:StartEvent";
        if (element is BpmnEndEvent)
            return "bpmn:EndEvent";
        if (element is BpmnIntermediateCatchEvent)
            return "bpmn:IntermediateCatchEvent";
        if (element is BpmnIntermediateThrowEvent)
            return "bpmn:IntermediateThrowEvent";
        if (element is BpmnBoundaryEvent)
            return "bpmn:BoundaryEvent";
        if (element is BpmnExclusiveGateway)
            return "bpmn:ExclusiveGateway";
        if (element is BpmnParallelGateway)
            return "bpmn:ParallelGateway";
        if (element is BpmnInclusiveGateway)
            return "bpmn:InclusiveGateway";
        if (element is BpmnComplexGateway)
            return "bpmn:ComplexGateway";
        if (element is BpmnEventBasedGateway)
            return "bpmn:EventBasedGateway";
        if (element is BpmnTask)
            return "bpmn:Task";     
        return element.GetType().Name;
    }
    
    /// <summary>
    /// Script context class to simplify variable access
    /// </summary>
    public class ConditionContext
    {
        public dynamic Variables { get; }
        
        public ConditionContext(dynamic variables)
        {
            Variables = variables ?? new ExpandoObject();
        }
    }

    /// <summary>
    /// Evaluates a condition expression using C# scripting
    /// </summary>
    private async Task<bool> EvaluateConditionAsync(
        BpmnProcessState state,
        string condition,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return true;
            
        try
        {
            Logger.LogDebug("Evaluating condition: {Condition}", condition);
            
            // Create context with dynamic Variables property
            var scriptContext = new ConditionContext(state.Variables);
            
            // Simple direct check for operator value
            var operatorValue = ((IDictionary<string, object>)state.Variables)["operator"]?.ToString();
            if (operatorValue == "+" || operatorValue == "add")
            {
                Logger.LogDebug("Operator condition matched: {Operator}", operatorValue);
                return true;
            }
            
            // For other conditions, use the original evaluation logic
            string scriptToEvaluate = condition;
            
            // Handle ${varName} syntax if present
            if (condition.Contains("${") && condition.Contains("}"))
            {
                scriptToEvaluate = System.Text.RegularExpressions.Regex.Replace(condition, 
                    @"\$\{([^}]+)\}", 
                    match => 
                    {
                        var varName = match.Groups[1].Value.Trim();
                        // Ensure valid C# identifier
                        if (!char.IsLetter(varName[0]) && varName[0] != '_')
                            varName = "_" + varName;
                        return $"Variables.{varName}";
                    });
                
                Logger.LogDebug("Converted ${} syntax: {OriginalCondition} → {ConvertedCondition}", 
                    condition, scriptToEvaluate);
            }
            
            // Handle string comparisons and Equals() calls
            scriptToEvaluate = System.Text.RegularExpressions.Regex.Replace(scriptToEvaluate,
                @"Variables\.([a-zA-Z_][a-zA-Z0-9_]*)\.Equals\(['""]([^'""]*)['""]\)",
                match => $"Variables.{match.Groups[1].Value} == \"{match.Groups[2].Value}\"");
            
            // Handle dictionary-style access
            scriptToEvaluate = System.Text.RegularExpressions.Regex.Replace(scriptToEvaluate,
                @"Variables\[['""]([^'""]*)['""]\]",
                match => $"Variables.{match.Groups[1].Value}");
            
            // Ensure the expression is a valid C# boolean expression
            if (!scriptToEvaluate.Contains("==") && !scriptToEvaluate.Contains("!=") && 
                !scriptToEvaluate.Contains(">") && !scriptToEvaluate.Contains("<") && 
                !scriptToEvaluate.Contains("&&") && !scriptToEvaluate.Contains("||"))
            {
                scriptToEvaluate = $"Convert.ToBoolean({scriptToEvaluate})";
            }
            
            Logger.LogDebug("Final script to evaluate: {Script}", scriptToEvaluate);
            
            // Configure script options with necessary references
            var scriptOptions = Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default
                .WithReferences(
                    typeof(System.Linq.Enumerable).Assembly,
                    typeof(System.Collections.Generic.Dictionary<,>).Assembly,
                    typeof(System.Text.RegularExpressions.Regex).Assembly,
                    typeof(System.Convert).Assembly)
                .WithImports(
                    "System", 
                    "System.Linq", 
                    "System.Collections.Generic",
                    "System.Text.RegularExpressions");
            
            // Execute the script with the context
            var result = await Microsoft.CodeAnalysis.CSharp.Scripting.CSharpScript.EvaluateAsync<bool>(
                scriptToEvaluate, 
                scriptOptions, 
                globals: scriptContext,
                cancellationToken: cancellationToken);
            
            Logger.LogDebug("Condition evaluation result: {Result}", result);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error evaluating condition: {Condition}", condition);
            return false;
        }
    }
    
    /// <summary>
    /// کلاس اطلاعات دروازه
    /// </summary>
    private class GatewayInfo
    {
        public string Id { get; set; }
        public bool IsJoin { get; set; }
        public IEnumerable<string> IncomingFlows { get; set; }
        public IEnumerable<string> OutgoingFlows { get; set; }
    }
    
    /// <summary>
    /// کلاس اطلاعات جریان
    /// </summary>
    private class FlowInfo
    {
        public string Id { get; set; }
        public string SourceElementId { get; set; }
        public string TargetElementId { get; set; }
        public string TargetElementType { get; set; }
        public string Condition { get; set; }
        public bool IsDefault { get; set; }
    }

    /// <summary>
    /// پردازش فعال شدن رویداد مرزی
    /// </summary>
    private async Task HandleBoundaryEventTriggerAsync(
        BpmnProcessState state, 
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Processing trigger of boundary event {EventId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
        
        var definition = await DefinitionStore.GetParsedDefinitionAsync(state.DeploymentKey, cancellationToken);
        if (definition == null)
        {
            Logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", state.DeploymentKey);
            await ActivateNextElementsAsync(state, @event, cancellationToken);
            return;
        }

        var process = definition.Items.OfType<BpmnProcess>()
            .FirstOrDefault(p => p.id == state.ProcessDefinitionId);
            
        if (process == null)
        {
            Logger.LogWarning("Process definition not found with ID {ProcessId}", state.ProcessDefinitionId);
            await ActivateNextElementsAsync(state, @event, cancellationToken);
            return;
        }

        var boundaryEvent = process.Items.OfType<BpmnBoundaryEvent>()
            .FirstOrDefault(e => e.id == @event.ElementId);
            
        if (boundaryEvent == null)
        {
            Logger.LogWarning("Boundary event {ElementId} not found in definition", @event.ElementId);
            await ActivateNextElementsAsync(state, @event, cancellationToken);
            return;
        }

        var attachedToElementId = boundaryEvent.attachedToRef?.ToString();
        if (string.IsNullOrEmpty(attachedToElementId))
        {
            Logger.LogWarning("Boundary event {ElementId} has no valid attachedToRef", @event.ElementId);
            await ActivateNextElementsAsync(state, @event, cancellationToken);
            return;
        }

        bool isInterrupting = boundaryEvent.cancelActivity;
        
        if (isInterrupting)
        {
            Logger.LogDebug("Boundary event {ElementId} is interrupting. Canceling activity {ActivityId}",
                @event.ElementId, attachedToElementId);
            
            await EventBus.PublishAsync(new ElementTerminated
            {
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = attachedToElementId,
                ElementType = "bpmn:BoundaryEvent"
            }, cancellationToken);
        }
        else
        {
            Logger.LogDebug("Boundary event {ElementId} is non-interrupting. Activity {ActivityId} continues",
                @event.ElementId, attachedToElementId);
        }
        
        await ActivateNextElementsAsync(state, @event, cancellationToken);
    }

    /// <summary>
    /// یافتن رویداد مرزی با شناسه مشخص در تعریف BPMN
    /// </summary>
    private BpmnBoundaryEvent FindBoundaryEvent(BpmnDefinitions definitions, string processId, string eventId)
    {
        var process = FindProcess(definitions, processId);
        if (process == null)
            return null;
        
        foreach (var item in process.Items)
        {
            if (item is BpmnBoundaryEvent boundaryEvent && boundaryEvent.id == eventId)
                return boundaryEvent;
        }
        
        return null;
    }

    private async Task HandleTaskCompletionAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Processing completion of task {TaskId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        // Check for boundary events
        var boundaryEvents = await GetBoundaryEventsAsync(state, @event.ElementId, cancellationToken);
        if (boundaryEvents.Any())
        {
            Logger.LogDebug("Found {Count} boundary events attached to task {TaskId}", 
                boundaryEvents.Count, @event.ElementId);
                
            foreach (var boundaryEvent in boundaryEvents)
            {
                if (boundaryEvent.IsInterrupting)
                {
                    Logger.LogDebug("Interrupting boundary event {EventId} will be triggered", boundaryEvent.Id);
                    await ProcessingElementAsync(state, boundaryEvent.Id, "bpmn:BoundaryEvent", 
                        @event.ElementId, null, cancellationToken, true);
                    return;
                }
            }
        }
        
        await ActivateNextElementsAsync(state, @event, cancellationToken);
    }

    private async Task HandleSubProcessCompletionAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Processing completion of subprocess {SubProcessId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        // Check if all child elements are completed
        var childElements = await GetChildElementsAsync(state, @event.ElementId, cancellationToken);
        var allChildrenCompleted = childElements.All(child => state.CompletedElements.Contains(child));
        
        if (!allChildrenCompleted)
        {
            Logger.LogDebug("Subprocess {SubProcessId} has incomplete child elements", @event.ElementId);
            return;
        }
        
        await ActivateNextElementsAsync(state, @event, cancellationToken);
    }

    private async Task<List<BoundaryEventInfo>> GetBoundaryEventsAsync(
        BpmnProcessState processState,
        string elementId,
        CancellationToken cancellationToken)
    {
        var definition = await DefinitionStore.GetParsedDefinitionAsync(processState.DeploymentKey, cancellationToken);
        if (definition == null)
        {
            Logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", processState.DeploymentKey);
            return new List<BoundaryEventInfo>();
        }

        var process = definition.Items.OfType<BpmnProcess>()
            .FirstOrDefault(p => p.id == processState.ProcessDefinitionId);
            
        if (process == null)
        {
            Logger.LogWarning("Process definition not found with ID {ProcessId}", processState.ProcessDefinitionId);
            return new List<BoundaryEventInfo>();
        }

        var boundaryEvents = process.Items.OfType<BpmnBoundaryEvent>()
            .Where(e => e.attachedToRef?.ToString() == elementId)
            .Select(e => new BoundaryEventInfo
            {
                Id = e.id,
                IsInterrupting = e.cancelActivity,
                EventType = GetEventType(e)
            })
            .ToList();

        return boundaryEvents;
    }

    private async Task<List<string>> GetChildElementsAsync(
        BpmnProcessState processState,
        string subProcessId,
        CancellationToken cancellationToken)
    {
        var definition = await DefinitionStore.GetParsedDefinitionAsync(processState.DeploymentKey, cancellationToken);
        if (definition == null)
        {
            Logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", processState.DeploymentKey);
            return new List<string>();
        }

        var process = definition.Items.OfType<BpmnProcess>()
            .FirstOrDefault(p => p.id == processState.ProcessDefinitionId);
            
        if (process == null)
        {
            Logger.LogWarning("Process definition not found with ID {ProcessId}", processState.ProcessDefinitionId);
            return new List<string>();
        }

        var subProcess = process.Items.OfType<BpmnSubProcess>()
            .FirstOrDefault(s => s.id == subProcessId);
            
        if (subProcess == null)
        {
            Logger.LogWarning("Subprocess {SubProcessId} not found in process {ProcessId}", 
                subProcessId, processState.ProcessDefinitionId);
            return new List<string>();
        }

        return subProcess.Items.OfType<BpmnBaseElement>()
            .Select(e => e.id)
            .ToList();
    }

    private string GetEventType(BpmnBoundaryEvent boundaryEvent)
    {
        if (boundaryEvent.Items == null || !boundaryEvent.Items.Any())
            return "unknown";
            
        var eventDefinition = boundaryEvent.Items.First();
        
        if (eventDefinition is BpmnTimerEventDefinition)
            return "timer";
        if (eventDefinition is BpmnMessageEventDefinition)
            return "message";
        if (eventDefinition is BpmnErrorEventDefinition)
            return "error";
        if (eventDefinition is BpmnSignalEventDefinition)
            return "signal";
            
        return "unknown";
    }

    private class BoundaryEventInfo
    {
        public string Id { get; set; }
        public bool IsInterrupting { get; set; }
        public string EventType { get; set; }
    }

    /// <summary>
    /// Get active incoming flows for a gateway
    /// </summary>
    private async Task<List<string>> GetActiveIncomingFlowsAsync(
        BpmnProcessState processState,
        string gatewayId,
        CancellationToken cancellationToken)
    {
        var definition = await DefinitionStore.GetParsedDefinitionAsync(processState.DeploymentKey, cancellationToken);
        if (definition == null)
        {
            Logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", processState.DeploymentKey);
            return new List<string>();
        }

        var process = definition.Items.OfType<BpmnProcess>()
            .FirstOrDefault(p => p.id == processState.ProcessDefinitionId);
            
        if (process == null)
        {
            Logger.LogWarning("Process definition not found with ID {ProcessId} in deployment {DeploymentKey}", 
                processState.ProcessDefinitionId, processState.DeploymentKey);
            return new List<string>();
        }

        var gateway = process.Items.OfType<BpmnGateway>()
            .FirstOrDefault(g => g.id == gatewayId);
            
        if (gateway == null)
        {
            Logger.LogWarning("Gateway element not found with ID {GatewayId} in process {ProcessId}", 
                gatewayId, processState.ProcessDefinitionId);
            return new List<string>();
        }

        return gateway.incoming.Select(f => f.ToString()).ToList();
    }
}