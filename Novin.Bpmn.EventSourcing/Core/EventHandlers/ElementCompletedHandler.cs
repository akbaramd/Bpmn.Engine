using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// Handles the completion of BPMN elements in a process instance
/// </summary>
public class ElementCompletedHandler : BaseEventHandler<ElementCompleted>
{
    private readonly IEventBus _eventBus;
    private readonly ScriptExecuter _scriptExecuter;

    /// <summary>
    /// Creates a new instance of ElementCompletedHandler
    /// </summary>
    public ElementCompletedHandler(
        IProcessInstanceStateStore stateStore,
        IEventStore eventStore,
        IProcessDeploymentStore definitionStore, 
        IEventBus eventBus,
        ILogger<ElementCompletedHandler> logger)
        : base(stateStore, eventStore, definitionStore, eventBus, logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _scriptExecuter = new ScriptExecuter();
    }

    /// <inheritdoc />
    protected override async Task ProcessEventAsync(
        ElementCompleted @event, 
        EventHandlerContext context, 
        CancellationToken cancellationToken)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        try
        {
            Logger.LogDebug("Processing ElementCompleted event for element {ElementId} in process {ProcessInstanceId}", 
                @event.ElementId, @event.InstanceId);

            // The execution should be in the context
            var execution = context.Execution;
            if (execution == null)
            {
                throw new InvalidOperationException($"Execution not found for element {@event.ElementId}");
            }
            
            // Get process definition using the deployment ID from the event
            var definition = await GetDefinitionsAsync(@event.DeploymentId, cancellationToken);
            if (definition == null)
            {
                throw new InvalidOperationException($"Process definition {@event.DeploymentKey} not found");
            }

            var definitionExplorer = GetDefiantionExplorer(definition);
            
            // Handle special element types with fork behavior
            if (IsGateway(@event.ElementType))
            {
                await HandleGatewayForkAsync(context, @event, definitionExplorer, cancellationToken);
            }
            else
            {
                // For non-gateway elements, just activate all outgoing flows
                await HandleStandardElementCompletionAsync(context, @event, definitionExplorer, cancellationToken);
            }
            
          
            
            // Sync variables from execution to process instance
            context.State.SyncVariablesFromExecution(execution);
            
            // Persist the updated state
            await StateStore.UpsertAsync(context.State, ct: cancellationToken);
            
            Logger.LogInformation("Element {ElementId} completed in process {ProcessInstanceId}", 
                @event.ElementId, @event.InstanceId);
                
            // Check if this is an end event and if the process is now complete
            if (@event.ElementType == BpmnElementType.EndEvent)
            {
                await CheckProcessCompletionAsync(context, @event, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling ElementCompleted event for element {ElementId} in process {ProcessInstanceId}",
                @event.ElementId, @event.InstanceId);
            throw;
        }
    }
    
    /// <summary>
    /// Handles the completion of a standard BPMN element (not a gateway)
    /// </summary>
    private async Task HandleStandardElementCompletionAsync(
        EventHandlerContext context,
        ElementCompleted @event,
        DefiantionExplorer definitionExplorer,
        CancellationToken cancellationToken)
    {
        // Find all outgoing sequence flows for this element
        var outgoingFlows = await FindOutgoingFlowsAsync(
            definitionExplorer, 
            @event.ProcessId, 
            @event.ElementId);
        
        if (outgoingFlows.Count == 0)
        {
            Logger.LogDebug("Element {ElementId} has no outgoing flows", @event.ElementId);
            return;
        }
        
        // For standard elements, activate all outgoing flows
        foreach (var flow in outgoingFlows)
        {
            await CreateTargetElementAsync(
                @event, 
                flow, 
                context.State,
                context.State.Variables, 
                isExecutable: true, 
                definitionExplorer, 
                cancellationToken);
        }
    }
    
    /// <summary>
    /// Handles fork behavior for BPMN gateways
    /// </summary>
    private async Task HandleGatewayForkAsync(
        EventHandlerContext context,
        ElementCompleted @event,
        DefiantionExplorer definitionExplorer,
        CancellationToken cancellationToken)
    {
        Logger.LogDebug("Handling fork for gateway {ElementId} of type {ElementType}", 
            @event.ElementId, @event.ElementType);
            
        // Store the state before handling fork to ensure any updates are preserved
        await StateStore.UpsertAsync(context.State, null, cancellationToken);
            
        // Find all outgoing sequence flows for this gateway
        var outgoingFlows = await FindOutgoingFlowsAsync(
            definitionExplorer, 
            @event.ProcessId, 
            @event.ElementId);
            
        if (outgoingFlows.Count == 0)
        {
            Logger.LogDebug("Gateway {ElementId} has no outgoing flows", @event.ElementId);
            return;
        }
        
        // Handle the gateway based on its type
        if (@event.ElementType == BpmnElementType.ParallelGateway)
        {
            // Parallel Gateway: Activate all outgoing flows
            foreach (var flow in outgoingFlows)
            {
                await CreateTargetElementAsync(
                    @event, 
                    flow, 
                    context.State,
                    context.State.Variables, 
                    isExecutable: true, 
                    definitionExplorer, 
                    cancellationToken);
            }
        }
        else if (@event.ElementType == BpmnElementType.ExclusiveGateway)
        {
            // Exclusive Gateway: Evaluate conditions and activate the first valid flow
            bool foundValidPath = false;
            
            // First, check for default flow
            var defaultFlow = outgoingFlows.FirstOrDefault(f => f.IsDefault);
            
            // Then check all flows with conditions
            foreach (var flow in outgoingFlows.Where(f => !f.IsDefault && !string.IsNullOrEmpty(f.Condition)))
            {
                // Evaluate the condition
                bool isValid = await _scriptExecuter.Evaluate(flow.Condition, context.Execution);
                
                if (isValid)
                {
                    // Create the target element with executable flag set to true
                    await CreateTargetElementAsync(
                        @event, 
                        flow, 
                        context.State,

                        context.State.Variables, 
                        isExecutable: true, 
                        definitionExplorer, 
                        cancellationToken);
                        
                    foundValidPath = true;
                    break;  // Stop after the first valid path
                }
            }
            
            // If no valid conditional path was found, use the default flow
            if (!foundValidPath && defaultFlow != null)
            {
                await CreateTargetElementAsync(
                    @event, 
                    defaultFlow, 
                    context.State,
                    context.State.Variables, 
                    isExecutable: true, 
                    definitionExplorer, 
                    cancellationToken);
            }
            
            // For all other flows, create non-executable elements
            foreach (var flow in outgoingFlows.Where(f => 
                (f != defaultFlow) && 
                (!foundValidPath || string.IsNullOrEmpty(f.Condition))))
            {
                await CreateTargetElementAsync(
                    @event, 
                    flow, 
                    context.State,
                    context.State.Variables, 
                    isExecutable: false, 
                    definitionExplorer, 
                    cancellationToken);
            }
        }
        else if (@event.ElementType == BpmnElementType.InclusiveGateway)
        {
            // Inclusive Gateway: Evaluate all conditions and activate all valid flows
            bool foundAnyValidPath = false;
            
            // First, evaluate all conditional flows
            foreach (var flow in outgoingFlows.Where(f => !string.IsNullOrEmpty(f.Condition)))
            {
                // Evaluate the condition
                bool isValid = await _scriptExecuter.Evaluate(flow.Condition, context.Execution);
                
                if (isValid)
                {
                    // Create the target element with executable flag set to true
                    await CreateTargetElementAsync(
                        @event, 
                        flow, 
                        context.State,
                        context.State.Variables, 
                        isExecutable: true, 
                        definitionExplorer, 
                        cancellationToken);
                        
                    foundAnyValidPath = true;
                }
                else
                {
                    // Create the target element with executable flag set to false
                    await CreateTargetElementAsync(
                        @event, 
                        flow, 
                        context.State,
                        context.State.Variables, 
                        isExecutable: false, 
                        definitionExplorer, 
                        cancellationToken);
                }
            }
            
            // If no path was valid, use the default flow
            var defaultFlow = outgoingFlows.FirstOrDefault(f => f.IsDefault);
            if (!foundAnyValidPath && defaultFlow != null)
            {
                await CreateTargetElementAsync(
                    @event, 
                    defaultFlow, 
                    context.State,
                    context.State.Variables, 
                    isExecutable: true, 
                    definitionExplorer, 
                    cancellationToken);
            }
            
            // Activate all flows without conditions (unconditional flows)
            foreach (var flow in outgoingFlows.Where(f => string.IsNullOrEmpty(f.Condition) && !f.IsDefault))
            {
                await CreateTargetElementAsync(
                    @event, 
                    flow, 
                    context.State,
                    context.State.Variables, 
                    isExecutable: true, 
                    definitionExplorer, 
                    cancellationToken);
            }
        }
        else
        {
            // For other gateway types or unknown gateways, activate all outgoing flows
            foreach (var flow in outgoingFlows)
            {
                await CreateTargetElementAsync(
                    @event, 
                    flow, 
                    context.State,
                    context.State.Variables, 
                    isExecutable: true, 
                    definitionExplorer, 
                    cancellationToken);
            }
        }
        
        // Store the state again after creating all target elements to ensure all executions are recorded
        await StateStore.UpsertAsync(context.State, null, cancellationToken);
    }
    
    /// <summary>
    /// Creates a target element for a sequence flow
    /// </summary>
    private async Task CreateTargetElementAsync(
        ElementCompleted sourceEvent,
        SequenceFlow flow,
        ProcessInstanceState state,
        Dictionary<string, object> variables,
        bool isExecutable,
        DefiantionExplorer definitionExplorer,
        CancellationToken cancellationToken)
    {
        // Find the target element
        var targetElementResult = await FindTargetElementAsync(
            definitionExplorer, 
            sourceEvent.ProcessId, 
            flow.TargetId);
            
        if (targetElementResult.ElementId == null)
        {
            Logger.LogWarning("Target element {TargetId} not found for flow {FlowId}", 
                flow.TargetId, flow.Id);
            return;
        }
        
        // Generate a unique execution ID for the element that will be created
        // This ensures that when the event is processed, it will create a new execution
        // with this ID rather than trying to find an existing one
  
        // create executions
        var execution = ElementExecutionBuilder.Init()
            .WithProcessInstanceId(sourceEvent.InstanceId)
            .WithElementId(flow.TargetId)
            .WithElementType(targetElementResult.ElementType)
            .WithLocalVariables(variables)
            .Executable(isExecutable)
            .Build()
            .BuildResult();
        
        state.AddExecution(execution);
        // Create and publish the ElementCreated event
        var createdEvent = new ElementCreated
        {
            InstanceId = sourceEvent.InstanceId,
            DeploymentKey = sourceEvent.DeploymentKey,
            DeploymentId = sourceEvent.DeploymentId,
            ProcessId = sourceEvent.ProcessId,
            ElementId = flow.TargetId,
            ElementType = targetElementResult.ElementType,
            SourceElementId = sourceEvent.ElementId,
            SequenceFlowId = flow.Id,
            IsExecutable = isExecutable,
            ExecutionId = execution.ExecutionId,  // Set the execution ID explicitly here
            Timestamp = DateTimeOffset.UtcNow
        };
        
        // Publish the event to create the target element
        PublishLater(createdEvent);
        
        Logger.LogDebug("Created {ExecutableStatus} element for target {TargetId} via flow {FlowId} with execution ID {ExecutionId}",
            isExecutable ? "executable" : "non-executable",
            flow.TargetId,
            flow.Id,
            execution.ExecutionId);
    }
    
    /// <summary>
    /// Checks if the process is complete after an end event is reached
    /// </summary>
    private async Task CheckProcessCompletionAsync(
        EventHandlerContext context,
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        // If we have no active executions left, complete the process
        if (!context.State.ActiveExecutions.Any())
        {
            Logger.LogInformation("All executions completed in process {ProcessInstanceId}, marking process as completed", 
                @event.InstanceId);
            
            
            
            // Publish process completed event
            PublishLater(new ProcessCompleted
            {
                InstanceId = @event.InstanceId,
                DeploymentKey = @event.DeploymentKey,
                DeploymentId = @event.DeploymentId,
                ProcessId = @event.ProcessId,
                Timestamp = DateTime.UtcNow
            });
        }
        else
        {
            Logger.LogDebug("Process {ProcessInstanceId} still has {Count} active executions", 
                @event.InstanceId, context.State.ActiveExecutions.Count);
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Finds all outgoing sequence flows for a specific element
    /// </summary>
    private async Task<List<SequenceFlow>> FindOutgoingFlowsAsync(
        DefiantionExplorer definitionExplorer,
        string processId,
        string elementId)
    {
        // This is a placeholder method - implement based on your definition explorer
        var result = new List<SequenceFlow>();
        
        // TODO: Implement this method using your definition explorer
        // It should find all sequence flows that have the specified elementId as their source
        var outgoingFlows = definitionExplorer.FindOutgoingSequenceFlows(processId, elementId); 
        await Task.CompletedTask;
        return outgoingFlows.Select(x => new SequenceFlow
        {
            Id = x.id,
            SourceId = x.sourceRef,
            TargetId = x.targetRef,
            Condition = x.conditionExpression?.Text.FirstOrDefault()??"",
        }).ToList();
    }
    
    /// <summary>
    /// Finds a target element by its ID
    /// </summary>
    private async Task<(string ElementId, BpmnElementType ElementType)> FindTargetElementAsync(
        DefiantionExplorer definitionExplorer,
        string processId,
        string elementId)
    {
        // This is a placeholder method - implement based on your definition explorer
        
        // TODO: Implement this method using your definition explorer
        // It should find the element with the specified ID and determine its type
        
        var targetElement = definitionExplorer.FindTargetElement(processId, elementId);
        return (targetElement.id, definitionExplorer.ConvertBpmnNodeToElementType(targetElement));
    }
    
    /// <summary>
    /// Checks if the element type is a gateway
    /// </summary>
    private bool IsGateway(BpmnElementType elementType)
    {
        return 
            elementType == BpmnElementType.ParallelGateway ||
            elementType == BpmnElementType.ExclusiveGateway ||
            elementType == BpmnElementType.InclusiveGateway;
    }
}

/// <summary>
/// Represents a BPMN sequence flow with its properties
/// </summary>
public class SequenceFlow
{
    /// <summary>
    /// The ID of the sequence flow
    /// </summary>
    public string Id { get; set; }
    
    /// <summary>
    /// The ID of the source element
    /// </summary>
    public string SourceId { get; set; }
    
    /// <summary>
    /// The ID of the target element
    /// </summary>
    public string TargetId { get; set; }
    
    /// <summary>
    /// The condition expression for the flow
    /// </summary>
    public string Condition { get; set; }
    
    /// <summary>
    /// Whether this is the default flow for a gateway
    /// </summary>
    public bool IsDefault { get; set; }
}