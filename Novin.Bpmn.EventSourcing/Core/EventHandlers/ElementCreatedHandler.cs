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
        
        _logger.LogInformation("Handling ElementCreated event for process instance {ProcessInstanceId}, element {ElementId}",
            @event.ProcessInstanceId, @event.ElementId);

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

            // Save state with retry
            await SaveStateWithRetryAsync(@event.ProcessInstanceId, state, (int)version, cancellationToken);

            // Publish ElementProcessing event
            await _eventBus.PublishAsync(new ElementProcessing
            {
                EventId = Guid.NewGuid(),
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId,
                ElementType = @event.ElementType,
                Progress = 0,
                ProcessingDetails = "Element created, starting processing",
                Timestamp = DateTime.UtcNow
            }, cancellationToken);

            _logger.LogInformation("Successfully handled ElementCreated event for element {ElementId}",
                @event.ElementId);

            // Handle different element types
            switch (@event.ElementType)
            {
                case "bpmn:StartEvent":
                    await HandleStartEventCreatedAsync(state, @event, cancellationToken);
                    break;
                    
                case "bpmn:EndEvent":
                    await HandleEndEventCreatedAsync(state, @event, cancellationToken);
                    break;
                    
                case "bpmn:UserTask":
                case "bpmn:ServiceTask":
                case "bpmn:ScriptTask":
                case "bpmn:BusinessRuleTask":
                case "bpmn:ManualTask":
                case "bpmn:ReceiveTask":
                case "bpmn:SendTask":
                    await HandleTaskCreatedAsync(state, @event, cancellationToken);
                    break;
                    
                case "bpmn:ParallelGateway":
                    await HandleParallelGatewayCreatedAsync(state, @event, cancellationToken);
                    break;
                    
                case "bpmn:InclusiveGateway":
                    await HandleInclusiveGatewayCreatedAsync(state, @event, cancellationToken);
                    break;
                    
                case "bpmn:ExclusiveGateway":
                    await HandleExclusiveGatewayCreatedAsync(state, @event, cancellationToken);
                    break;
                    
                case "bpmn:EventBasedGateway":
                    await HandleEventBasedGatewayCreatedAsync(state, @event, cancellationToken);
                    break;
                    
                case "bpmn:SubProcess":
                    await HandleSubProcessCreatedAsync(state, @event, cancellationToken);
                    break;
                    
                case "bpmn:CallActivity":
                    await HandleCallActivityCreatedAsync(state, @event, cancellationToken);
                    break;
                    
                default:
                    _logger.LogDebug("Element {ElementId} of type {ElementType} created in process {ProcessInstanceId}", 
                        @event.ElementId, @event.ElementType, @event.ProcessInstanceId);
                    await TransitionToProcessingAsync(state, @event, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling ElementCreated event for element {ElementId}",
                @event.ElementId);
            throw;
        }
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
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing creation of start event {EventId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        await TransitionToProcessingAsync(state, @event, cancellationToken);
    }
    
    private async Task HandleEndEventCreatedAsync(
        BpmnProcessState state,
        ElementCreated @event,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing creation of end event {EventId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        await TransitionToProcessingAsync(state, @event, cancellationToken);
    }
    
    private async Task HandleTaskCreatedAsync(
        BpmnProcessState state,
        ElementCreated @event,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing creation of task {TaskId} of type {TaskType} in process {ProcessInstanceId}",
            @event.ElementId, @event.ElementType, @event.ProcessInstanceId);
            
        await TransitionToProcessingAsync(state, @event, cancellationToken);
    }
    
    private async Task HandleParallelGatewayCreatedAsync(
        BpmnProcessState state,
        ElementCreated @event,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing creation of parallel gateway {GatewayId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        var gatewayInfo = await GetGatewayInfoAsync(state, @event.ElementId, cancellationToken);
        
        if (gatewayInfo.IsJoin)
        {
            // For join gateways, check if we can proceed
            bool canMerge = await CanMergeParallelGatewayAsync(state, @event, gatewayInfo, cancellationToken);
            
            if (canMerge)
            {
                _logger.LogDebug("Parallel gateway {GatewayId} can proceed with merge in process {ProcessInstanceId}",
                    @event.ElementId, @event.ProcessInstanceId);
                    
                // Remove all incoming flow tokens before proceeding
                foreach (var flowId in gatewayInfo.IncomingFlows)
                {
                    state.CompletedElements.Remove(flowId);
                }
                
                // Save state after removing tokens
                var (_, currentVersion) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
                await _stateStore.SaveStateAsync(@event.ProcessInstanceId, state, currentVersion + 1);
                
                await TransitionToProcessingAsync(state, @event, cancellationToken);
            }
            else
            {
                _logger.LogDebug("Parallel gateway {GatewayId} is waiting for more tokens in process {ProcessInstanceId}. Current tokens: {CurrentTokens}, Required: {RequiredTokens}",
                    @event.ElementId, @event.ProcessInstanceId, 
                    state.CompletedElements.Count(e => e == @event.ElementId),
                    gatewayInfo.IncomingFlows.Count());
            }
        }
        else
        {
            // For split gateways, proceed to processing
            await TransitionToProcessingAsync(state, @event, cancellationToken);
        }
    }
    
    private async Task HandleInclusiveGatewayCreatedAsync(
        BpmnProcessState state,
        ElementCreated @event,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing creation of inclusive gateway {GatewayId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        var gatewayInfo = await GetGatewayInfoAsync(state, @event.ElementId, cancellationToken);
        
        if (gatewayInfo.IsJoin)
        {
            // For join gateways, check if we can proceed
            bool canMerge = await CanMergeInclusiveGatewayAsync(state, @event, gatewayInfo, cancellationToken);
            
            if (canMerge)
            {
                _logger.LogDebug("Inclusive gateway {GatewayId} can proceed with merge in process {ProcessInstanceId}",
                    @event.ElementId, @event.ProcessInstanceId);
                    
                // Remove tokens from active incoming flows
                var activeFlows = await GetActiveIncomingFlowsAsync(state, @event.ElementId, cancellationToken);
                foreach (var flowId in activeFlows)
                {
                    state.CompletedElements.Remove(flowId);
                }
                
                // Save state after removing tokens
                var (_, currentVersion) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
                await _stateStore.SaveStateAsync(@event.ProcessInstanceId, state, currentVersion + 1);
                
                await TransitionToProcessingAsync(state, @event, cancellationToken);
            }
            else
            {
                _logger.LogDebug("Inclusive gateway {GatewayId} is waiting for more tokens in process {ProcessInstanceId}. Current tokens: {CurrentTokens}, Required: {RequiredTokens}",
                    @event.ElementId, @event.ProcessInstanceId,
                    state.CompletedElements.Count(e => e == @event.ElementId),
                    (await GetActiveIncomingFlowsAsync(state, @event.ElementId, cancellationToken)).Count);
            }
        }
        else
        {
            // For split gateways, proceed to processing
            await TransitionToProcessingAsync(state, @event, cancellationToken);
        }
    }
    
    private async Task HandleExclusiveGatewayCreatedAsync(
        BpmnProcessState state,
        ElementCreated @event,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing creation of exclusive gateway {GatewayId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        var gatewayInfo = await GetGatewayInfoAsync(state, @event.ElementId, cancellationToken);
        
        if (gatewayInfo.IsJoin)
        {
            // For XOR-join gateways, check if we have at least one token
            var hasToken = state.CompletedElements.Any(e => e == @event.ElementId);
            
            if (hasToken)
            {
                _logger.LogDebug("Exclusive gateway {GatewayId} has received a token, proceeding with merge in process {ProcessInstanceId}",
                    @event.ElementId, @event.ProcessInstanceId);
                    
                // Remove the token before proceeding
                state.CompletedElements.Remove(@event.ElementId);
                
                // Save state after removing token
                var (_, currentVersion) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
                await _stateStore.SaveStateAsync(@event.ProcessInstanceId, state, currentVersion + 1);
                
                await TransitionToProcessingAsync(state, @event, cancellationToken);
            }
            else
            {
                _logger.LogDebug("Exclusive gateway {GatewayId} is waiting for a token in process {ProcessInstanceId}",
                    @event.ElementId, @event.ProcessInstanceId);
            }
        }
        else
        {
            // For split gateways, proceed to processing
            await TransitionToProcessingAsync(state, @event, cancellationToken);
        }
    }
    
    private async Task HandleEventBasedGatewayCreatedAsync(
        BpmnProcessState state,
        ElementCreated @event,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing creation of event-based gateway {GatewayId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        await TransitionToProcessingAsync(state, @event, cancellationToken);
    }
    
    private async Task HandleSubProcessCreatedAsync(
        BpmnProcessState state,
        ElementCreated @event,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing creation of subprocess {SubProcessId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        await TransitionToProcessingAsync(state, @event, cancellationToken);
    }
    
    private async Task HandleCallActivityCreatedAsync(
        BpmnProcessState state,
        ElementCreated @event,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing creation of call activity {CallActivityId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        await TransitionToProcessingAsync(state, @event, cancellationToken);
    }
    
    private async Task TransitionToProcessingAsync(
        BpmnProcessState state,
        ElementCreated @event,
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
            ProcessingDetails = "Initial processing"
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
    
    private async Task<bool> CanMergeParallelGatewayAsync(
        BpmnProcessState state,
        ElementCreated @event,
        GatewayInfo gatewayInfo,
        CancellationToken cancellationToken)
    {
        var receivedTokensCount = state.CompletedElements.Count(e => e == @event.ElementId);
            
        if (!state.CompletedElements.Contains(@event.ElementId))
        {
            receivedTokensCount++;
        }
        
        var requiredTokens = gatewayInfo.IncomingFlows.Count();
        
        _logger.LogDebug("Parallel gateway {GatewayId} has received {ReceivedCount} tokens out of {TotalCount} required",
            @event.ElementId, receivedTokensCount, requiredTokens);
            
        return receivedTokensCount >= requiredTokens;
    }
    
    private async Task<bool> CanMergeInclusiveGatewayAsync(
        BpmnProcessState state,
        ElementCreated @event,
        GatewayInfo gatewayInfo,
        CancellationToken cancellationToken)
    {
        var activeFlows = await GetActiveIncomingFlowsAsync(state, @event.ElementId, cancellationToken);
        var receivedTokensCount = state.CompletedElements.Count(e => e == @event.ElementId);
            
        if (!state.CompletedElements.Contains(@event.ElementId))
        {
            receivedTokensCount++;
        }
        
        _logger.LogDebug("Inclusive gateway {GatewayId} has received {ReceivedCount} tokens out of {ActiveCount} active paths",
            @event.ElementId, receivedTokensCount, activeFlows.Count);
            
        return receivedTokensCount >= activeFlows.Count;
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
} 