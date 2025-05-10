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
/// پردازش‌کننده رویداد تکمیل المان
/// این پردازش‌کننده مسئول ادامه جریان فرآیند پس از تکمیل یک المان است
/// </summary>
public class ElementCompletedHandler : IBpmnEventHandler<ElementCompleted>
{
    private const int MaxRetries = 3;
    private const int RetryDelay = 1000; // 1 second

    private readonly ILogger<ElementCompletedHandler> _logger;
    private readonly IStateStore _stateStore;
    private readonly IEventBus _eventBus;
    private readonly IBpmnDefinitionStorage _definitionStorage;
    
    /// <summary>
    /// ایجاد یک نمونه جدید از پردازش‌کننده رویداد تکمیل المان
    /// </summary>
    /// <param name="logger">سیستم ثبت وقایع</param>
    /// <param name="stateStore">مخزن وضعیت</param>
    /// <param name="eventBus">گذرگاه رویداد</param>
    /// <param name="definitionStorage">مخزن تعاریف BPMN</param>
    public ElementCompletedHandler(
        ILogger<ElementCompletedHandler> logger,
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
    public async Task HandleAsync(ElementCompleted @event, CancellationToken cancellationToken = default)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }
        
        try
        {
            _logger.LogDebug("Processing ElementCompleted event for element {ElementId} in process {ProcessInstanceId}", 
                @event.ElementId, @event.ProcessInstanceId);
            
            var retryCount = 0;
            while (true)
            {
                try
                {
                    var (state, version) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
                    
                    if (state == null)
                    {
                        _logger.LogWarning("Process instance state not found for {ProcessInstanceId}", @event.ProcessInstanceId);
                        return;
                    }

                    // Add the completed element to the state
                    if (!state.CompletedElements.Contains(@event.ElementId))
                    {
                        state.CompletedElements.Add(@event.ElementId);
                    }
                    
                    // Remove from active elements if present
                    state.ActiveElements.Remove(@event.ElementId);
                    
                    // Update element status
                    if (state.ElementStatuses.TryGetValue(@event.ElementId, out var elementStatus))
                    {
                        elementStatus.Status = "Completed";
                        elementStatus.CompletedAt = DateTime.UtcNow;
                        elementStatus.UpdatedAt = DateTime.UtcNow;
                    }
                    
                    // Save the updated state with the current version
                    await _stateStore.SaveStateAsync(@event.ProcessInstanceId, state, version);

                    // Check if this is an end event
                    if (@event.ElementType == "bpmn:EndEvent")
                    {
                        await HandleEndEventCompletionAsync(state, @event, cancellationToken);
                        return;
                    }

                    // Get outgoing flows
                    var outgoingFlows = await GetOutgoingFlowsAsync(state, @event.ElementId, cancellationToken);
                    if (!outgoingFlows.Any())
                    {
                        _logger.LogDebug("No outgoing flows found for element {ElementId} in process {ProcessInstanceId}",
                            @event.ElementId, @event.ProcessInstanceId);
                        return;
                    }

                    // Handle different element types
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
                            _logger.LogDebug("Element {ElementId} of type {ElementType} completed in process {ProcessInstanceId}", 
                                @event.ElementId, @event.ElementType, @event.ProcessInstanceId);
                            await ActivateNextElementsAsync(state, @event, cancellationToken);
                            break;
                    }
                    
                    return; // Success, exit retry loop
                }
                catch (ConcurrencyException)
                {
                    retryCount++;
                    if (retryCount >= MaxRetries)
                    {
                        _logger.LogError("Failed to handle ElementCompleted event after {MaxRetries} retries for element {ElementId} in process {ProcessInstanceId}",
                            MaxRetries, @event.ElementId, @event.ProcessInstanceId);
                        throw;
                    }
                    
                    _logger.LogWarning("Concurrency conflict detected, retry {RetryCount} of {MaxRetries} for element {ElementId} in process {ProcessInstanceId}",
                        retryCount, MaxRetries, @event.ElementId, @event.ProcessInstanceId);
                    
                    await Task.Delay(RetryDelay * retryCount, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling ElementCompleted event for element {ElementId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            throw;
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
        _logger.LogDebug("Processing completion of start event {EventId} in process {ProcessInstanceId}",
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
        _logger.LogDebug("Processing completion of end event {EventId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        await _eventBus.PublishAsync(new ProcessCompletedEvent
        {
            ProcessInstanceId = @event.ProcessInstanceId,
            EndEventId = @event.ElementId
        }, cancellationToken);
        
        _logger.LogInformation("Process {ProcessInstanceId} completed with end event {EndEventId}", 
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
        _logger.LogDebug("Processing completion of user task {TaskId} in process {ProcessInstanceId}",
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
        _logger.LogDebug("Processing completion of service task {TaskId} in process {ProcessInstanceId}",
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
            bool canMerge = await CanMergeParallelGatewayAsync(state, @event, gatewayInfo, cancellationToken);
            
            if (canMerge)
            {
                _logger.LogDebug("Parallel gateway {GatewayId} has received all required tokens. Proceeding with merge in process {ProcessInstanceId}",
                    @event.ElementId, @event.ProcessInstanceId);
                
                await ActivateNextElementsAsync(state, @event, cancellationToken);
            }
            else
            {
                _logger.LogDebug("Parallel gateway {GatewayId} is waiting for more tokens to merge in process {ProcessInstanceId}",
                    @event.ElementId, @event.ProcessInstanceId);
            }
        }
        else
        {
            _logger.LogDebug("Parallel gateway {GatewayId} is forking execution in process {ProcessInstanceId}",
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
            var canMerge = await CanMergeInclusiveGatewayAsync(state, @event, gatewayInfo, cancellationToken);
            
            if (canMerge)
            {
                _logger.LogDebug("Inclusive gateway {GatewayId} has received all required tokens from active paths. Proceeding with merge in process {ProcessInstanceId}",
                    @event.ElementId, @event.ProcessInstanceId);
                
                await ActivateNextElementsAsync(state, @event, cancellationToken);
            }
            else
            {
                _logger.LogDebug("Inclusive gateway {GatewayId} is waiting for more tokens from active paths in process {ProcessInstanceId}",
                    @event.ElementId, @event.ProcessInstanceId);
            }
        }
        else
        {
            _logger.LogDebug("Inclusive gateway {GatewayId} is evaluating conditions for outgoing paths in process {ProcessInstanceId}",
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
            _logger.LogDebug("Exclusive gateway {GatewayId} received a token. As an XOR-join, proceeding immediately in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            
            await ActivateNextElementsAsync(state, @event, cancellationToken);
        }
        else
        {
            _logger.LogDebug("Exclusive gateway {GatewayId} is evaluating conditions to select exactly one path in process {ProcessInstanceId}",
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
            _logger.LogDebug("No outgoing flows found for element {ElementId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            return;
        }
        
        foreach (var flow in outgoingFlows)
        {
            await ActivateElementAsync(state, flow.TargetElementId, flow.TargetElementType, 
                @event.ElementId, flow.Id, cancellationToken);
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
            _logger.LogDebug("No outgoing flows found for parallel gateway {GatewayId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            return;
        }
        
        _logger.LogDebug("Activating all {FlowCount} outgoing flows for parallel gateway {GatewayId} in process {ProcessInstanceId}",
            outgoingFlows.Count, @event.ElementId, @event.ProcessInstanceId);
            
        foreach (var flow in outgoingFlows)
        {
            await ActivateElementAsync(state, flow.TargetElementId, flow.TargetElementType, 
                @event.ElementId, flow.Id, cancellationToken);
        }
    }
    
    /// <summary>
    /// فعال‌سازی مسیرهای خروجی بر اساس شرط (برای دروازه‌های انحصاری و فراگیر)
    /// </summary>
    private async Task ActivateConditionalOutgoingFlowsAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        GatewayInfo gatewayInfo,
        bool activateAllValidPaths,
        CancellationToken cancellationToken)
    {
        var outgoingFlows = await GetOutgoingFlowsAsync(state, @event.ElementId, cancellationToken);
        
        if (outgoingFlows == null || !outgoingFlows.Any())
        {
            _logger.LogDebug("No outgoing flows found for gateway {GatewayId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            return;
        }
        
        _logger.LogDebug("Evaluating conditions for {FlowCount} outgoing flows from gateway {GatewayId} in process {ProcessInstanceId}",
            outgoingFlows.Count, @event.ElementId, @event.ProcessInstanceId);
            
        var defaultFlow = outgoingFlows.FirstOrDefault(f => f.IsDefault);
        var validFlows = new List<FlowInfo>();
        
        foreach (var flow in outgoingFlows.Where(f => !f.IsDefault))
        {
            if (string.IsNullOrEmpty(flow.Condition))
            {
                _logger.LogDebug("Flow {FlowId} has no condition, treating as valid", flow.Id);
                validFlows.Add(flow);
            }
            else
            {
                var isValid = await EvaluateConditionAsync(state, flow.Condition, cancellationToken);
                _logger.LogDebug("Flow {FlowId} condition evaluation result: {IsValid}", flow.Id, isValid);
                
                if (isValid)
                {
                    validFlows.Add(flow);
                    
                    if (!activateAllValidPaths)
                    {
                        _logger.LogDebug("Exclusive gateway taking only first valid path: {FlowId}", flow.Id);
                        break;
                    }
                }
            }
        }
        
        if (!validFlows.Any() && defaultFlow != null)
        {
            _logger.LogDebug("No valid paths found, taking default flow: {FlowId}", defaultFlow.Id);
            validFlows.Add(defaultFlow);
        }
        
        if (!validFlows.Any())
        {
            _logger.LogWarning("No valid paths found and no default flow for gateway {GatewayId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
                
            await _eventBus.PublishAsync(new ElementFailed
            {
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId,
                ElementType = @event.ElementType,
                ErrorMessage = "No valid flow found for gateway"
            }, cancellationToken);
            return;
        }
        
        foreach (var flow in validFlows)
        {
            await ActivateElementAsync(state, flow.TargetElementId, flow.TargetElementType, 
                @event.ElementId, flow.Id, cancellationToken);
        }
    }
    
    /// <summary>
    /// فعال‌سازی یک المان
    /// </summary>
    private async Task ActivateElementAsync(
        BpmnProcessState state,
        string elementId,
        string elementType,
        string sourceElementId,
        string sequenceFlowId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Activating element {ElementId} of type {ElementType} via flow {FlowId} in process {ProcessInstanceId}",
            elementId, elementType, sequenceFlowId, state.ProcessInstanceId);
            
        var retryCount = 0;
        while (true)
        {
            try
            {
                // Get current state and version
                var (currentState, currentVersion) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(state.ProcessInstanceId);
                
                if (currentState == null)
                {
                    _logger.LogWarning("Process instance state not found for {ProcessInstanceId}", state.ProcessInstanceId);
                    return;
                }

                // Add to active elements if not already present
                if (!currentState.ActiveElements.Contains(elementId))
                {
                    currentState.ActiveElements.Add(elementId);
                }
                
                // Save the updated state with current version
                await _stateStore.SaveStateAsync(state.ProcessInstanceId, currentState, currentVersion);
                
                // First publish ElementCreated event
                await _eventBus.PublishAsync(new ElementCreated
                {
                    ProcessInstanceId = state.ProcessInstanceId,
                    ElementId = elementId,
                    ElementType = elementType,
                    EventId = Guid.NewGuid(),
                    Timestamp = DateTime.UtcNow
                }, cancellationToken);
                
   
                
                return; // Success, exit retry loop
            }
            catch (ConcurrencyException)
            {
                retryCount++;
                if (retryCount >= MaxRetries)
                {
                    _logger.LogError("Failed to activate element {ElementId} after {MaxRetries} retries in process {ProcessInstanceId}",
                        elementId, MaxRetries, state.ProcessInstanceId);
                    throw;
                }
                
                _logger.LogWarning("Concurrency conflict detected while activating element {ElementId}, retry {RetryCount} of {MaxRetries} in process {ProcessInstanceId}",
                    elementId, retryCount, MaxRetries, state.ProcessInstanceId);
                
                await Task.Delay(RetryDelay * retryCount, cancellationToken);
            }
        }
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
        
        _logger.LogDebug("Parallel gateway {GatewayId} has received {ReceivedCount} tokens out of {TotalCount} required",
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
        var definitions = _definitionStorage.GetParsedDefinition(state.DeploymentKey);
        if (definitions == null)
        {
            _logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", state.DeploymentKey);
            return false;
        }
        
        var process = FindProcess(definitions, state.ProcessDefinitionId);
        if (process == null)
        {
            _logger.LogWarning("Process definition not found with ID {ProcessId} in deployment {DeploymentKey}", 
                state.ProcessDefinitionId, state.DeploymentKey);
            return false;
        }
        
        var activeIncomingFlows = DetermineActiveIncomingFlows(process, state, @event.ElementId);
        var receivedTokensCount = state.CompletedElements.Count(e => e == @event.ElementId);
            
        if (!state.CompletedElements.Contains(@event.ElementId))
        {
            receivedTokensCount++;
        }
        
        _logger.LogDebug("Inclusive gateway {GatewayId} has received {ReceivedCount} tokens out of {ActiveCount} active paths",
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
    
    /// <summary>
    /// دریافت جریان‌های خروجی از یک المان
    /// </summary>
    private async Task<List<FlowInfo>> GetOutgoingFlowsAsync(
        BpmnProcessState state,
        string elementId,
        CancellationToken cancellationToken)
    {
        var definitions = _definitionStorage.GetParsedDefinition(state.DeploymentKey);
        if (definitions == null)
        {
            _logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", state.DeploymentKey);
            return new List<FlowInfo>();
        }

        var process = FindProcess(definitions, state.ProcessDefinitionId);
        if (process == null)
        {
            _logger.LogWarning("Process definition not found with ID {ProcessId} in deployment {DeploymentKey}", 
                state.ProcessDefinitionId, state.DeploymentKey);
            return new List<FlowInfo>();
        }

        var outgoingFlows = FindOutgoingFlows(process, elementId);
        if (!outgoingFlows.Any())
        {
            _logger.LogDebug("No outgoing flows found for element {ElementId} in process {ProcessId}", 
                elementId, state.ProcessDefinitionId);
            return new List<FlowInfo>();
        }

        var result = new List<FlowInfo>();

        foreach (var flow in outgoingFlows)
        {
            var targetElement = FindElementById(process, flow.targetRef);
            if (targetElement == null)
            {
                _logger.LogWarning("Target element not found with ID {TargetId} for flow {FlowId}", 
                    flow.targetRef, flow.id);
                continue;
            }

            var targetElementType = GetElementType(targetElement);
            bool isDefault = false;
            
            var sourceElement = FindElementById(process, flow.sourceRef);
            if (sourceElement is BpmnGateway gateway)
            {
                if (gateway is BpmnExclusiveGateway exclusiveGateway)
                {
                    isDefault = exclusiveGateway.@default == flow.id;
                }
                else if (gateway is BpmnInclusiveGateway inclusiveGateway)
                {
                    isDefault = inclusiveGateway.@default == flow.id;
                }
            }

            result.Add(new FlowInfo
            {
                Id = flow.id,
                SourceElementId = flow.sourceRef,
                TargetElementId = flow.targetRef,
                TargetElementType = targetElementType,
                Condition = flow.conditionExpression?.Text.ToString(),
                IsDefault = isDefault
            });
        }

        return result;
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
    /// ارزیابی یک شرط
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
            if (condition.Contains("=="))
            {
                var parts = condition.Split(new[] { "==" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var leftPart = parts[0].Trim();
                    var rightPart = parts[1].Trim();
                    
                    if (leftPart.StartsWith("${") && leftPart.EndsWith("}"))
                    {
                        var varName = leftPart.Substring(2, leftPart.Length - 3);
                        if (state.Variables.TryGetValue(varName, out var value))
                        {
                            return value?.ToString() == rightPart.Trim('"', '\'');
                        }
                    }
                }
            }
            
            _logger.LogWarning("Condition evaluation not fully implemented: {Condition}", condition);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating condition: {Condition}", condition);
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
        _logger.LogDebug("Processing trigger of boundary event {EventId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
        
        var definition = _definitionStorage.GetParsedDefinition(state.DeploymentKey);
        if (definition == null)
        {
            _logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", state.DeploymentKey);
            await ActivateNextElementsAsync(state, @event, cancellationToken);
            return;
        }

        var boundaryEvent = FindBoundaryEvent(definition, state.ProcessDefinitionId, @event.ElementId);
        if (boundaryEvent == null)
        {
            _logger.LogWarning("Boundary event {ElementId} not found in definition", @event.ElementId);
            await ActivateNextElementsAsync(state, @event, cancellationToken);
            return;
        }

        string attachedToElementId = boundaryEvent.attachedToRef?.ToString();
        if (string.IsNullOrEmpty(attachedToElementId))
        {
            _logger.LogWarning("Boundary event {ElementId} has no valid attachedToRef", @event.ElementId);
            await ActivateNextElementsAsync(state, @event, cancellationToken);
            return;
        }

        bool isInterrupting = boundaryEvent.cancelActivity;
        
        if (isInterrupting)
        {
            _logger.LogDebug("Boundary event {ElementId} is interrupting. Canceling activity {ActivityId}",
                @event.ElementId, attachedToElementId);
            
            await _eventBus.PublishAsync(new ElementTerminated
            {
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = attachedToElementId,
                ElementType = "bpmn:BoundaryEvent"
            }, cancellationToken);
        }
        else
        {
            _logger.LogDebug("Boundary event {ElementId} is non-interrupting. Activity {ActivityId} continues",
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
        _logger.LogDebug("Processing completion of task {TaskId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        // Check for boundary events
        var boundaryEvents = await GetAttachedBoundaryEventsAsync(state, @event.ElementId, cancellationToken);
        if (boundaryEvents.Any())
        {
            _logger.LogDebug("Found {Count} boundary events attached to task {TaskId}", 
                boundaryEvents.Count, @event.ElementId);
                
            foreach (var boundaryEvent in boundaryEvents)
            {
                if (boundaryEvent.IsInterrupting)
                {
                    _logger.LogDebug("Interrupting boundary event {EventId} will be triggered", boundaryEvent.Id);
                    await ActivateElementAsync(state, boundaryEvent.Id, "bpmn:BoundaryEvent", 
                        @event.ElementId, null, cancellationToken);
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
        _logger.LogDebug("Processing completion of subprocess {SubProcessId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        // Check if all child elements are completed
        var childElements = await GetChildElementsAsync(state, @event.ElementId, cancellationToken);
        var allChildrenCompleted = childElements.All(child => state.CompletedElements.Contains(child));
        
        if (!allChildrenCompleted)
        {
            _logger.LogDebug("Subprocess {SubProcessId} has incomplete child elements", @event.ElementId);
            return;
        }
        
        await ActivateNextElementsAsync(state, @event, cancellationToken);
    }

    private async Task<List<BoundaryEventInfo>> GetAttachedBoundaryEventsAsync(
        BpmnProcessState state,
        string elementId,
        CancellationToken cancellationToken)
    {
        var definition = _definitionStorage.GetParsedDefinition(state.DeploymentKey);
        if (definition == null)
        {
            _logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", state.DeploymentKey);
            return new List<BoundaryEventInfo>();
        }

        var process = FindProcess(definition, state.ProcessDefinitionId);
        if (process == null)
        {
            _logger.LogWarning("Process definition not found with ID {ProcessId}", state.ProcessDefinitionId);
            return new List<BoundaryEventInfo>();
        }

        var boundaryEvents = process.Items.OfType<BpmnBoundaryEvent>()
            .Where(e => e.attachedToRef?.Name == elementId)
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
        BpmnProcessState state,
        string subProcessId,
        CancellationToken cancellationToken)
    {
        var definition = _definitionStorage.GetParsedDefinition(state.DeploymentKey);
        if (definition == null)
        {
            _logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", state.DeploymentKey);
            return new List<string>();
        }

        var process = FindProcess(definition, state.ProcessDefinitionId);
        if (process == null)
        {
            _logger.LogWarning("Process definition not found with ID {ProcessId}", state.ProcessDefinitionId);
            return new List<string>();
        }

        var subProcess = process.Items.OfType<BpmnSubProcess>()
            .FirstOrDefault(s => s.id == subProcessId);
            
        if (subProcess == null)
        {
            _logger.LogWarning("Subprocess {SubProcessId} not found in process {ProcessId}", 
                subProcessId, state.ProcessDefinitionId);
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
}