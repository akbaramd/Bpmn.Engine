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
/// Handles the initial creation and activation of BPMN elements
/// Manages gateway merges and transitions to processing state
/// </summary>
public class ElementCreatedHandler : BaseEventHandler<ElementCreated>
{
    private readonly IEventBus _eventBus;

    /// <summary>
    /// Creates a new instance of ElementCreatedHandler
    /// </summary>
    public ElementCreatedHandler(
        IProcessInstanceStateStore stateStore,
        IEventStore eventStore,
        IProcessDeploymentStore definitionStore,
        IEventBus eventBus,
        ILogger<ElementCreatedHandler> logger)
        : base(stateStore, eventStore, definitionStore, eventBus, logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    /// <inheritdoc />
    /// 
    // before presse event
    // 1. check if the element is already in the state

 
    protected override async Task ProcessEventAsync(
        ElementCreated @event,
        EventHandlerContext context,
        CancellationToken cancellationToken)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        Logger.LogInformation("Handling ElementCreated event for process instance {ProcessInstanceId}, element {ElementId}",
            @event.InstanceId, @event.ElementId);

        try
        {
            // Get process definition using the process definition ID from the event
            var definition = await GetDefinitionsAsync(@event.DeploymentId, cancellationToken);

            if (definition == null)
            {
                throw new InvalidOperationException($"Process definition {@event.DeploymentKey} not found");
            }

            var definitionExplorer = GetDefiantionExplorer(definition);
            
            // Get the execution from context - it should have been created in PrepareContextAsync in BaseEventHandler
            var execution = context.CurrentExecution;
            if (execution == null)
            {
                throw new InvalidOperationException($"Execution not found for element {@event.ElementId}");
            }
            
            Logger.LogDebug("Found execution {ExecutionId} for element {ElementId}", 
                execution.ExecutionId, @event.ElementId);
                
            // Save the initial executable status from the event
            bool initialIsExecutable = @event.IsExecutable;
            
            // Handle gateway merges
            if (await HandleMergeGatewayIfNeededAsync(@event, context, definitionExplorer, cancellationToken))
            {
                // Merge gateway handling complete
                return;
            }
       
            // Handle based on element type and whether it's executable
            // Check the execution.IsExecutable which may have been updated by the gateway merge logic
            if (execution.IsExecutable)
            {
                // For executable elements, determine handling based on element type
                Logger.LogDebug("Processing executable element {ElementId} of type {ElementType}",
                    @event.ElementId, @event.ElementType);

                // Create and publish the appropriate processing event based on element type
                await PublishSpecializedProcessingEvent(
                    @event,
                    execution.ExecutionId,
                    definitionExplorer,
                    cancellationToken);
            }
            else
            {
                // Publish completion event for non-executable paths
                PublishLater(new ElementCompleted
                {
                    ProcessId = @event.ProcessId,
                    InstanceId = @event.InstanceId,
                    DeploymentKey = @event.DeploymentKey,
                    DeploymentId = @event.DeploymentId,
                    ElementId = @event.ElementId,
                    ElementType = @event.ElementType,
                    ExecutionId = execution.ExecutionId,
                    IsExecutable = false,
                    Timestamp = DateTimeOffset.UtcNow
                });
                
                Logger.LogDebug("Element {ElementId} is non-executable, completing immediately", @event.ElementId);
            }

            Logger.LogDebug("Successfully processed ElementCreated event for {ElementId}", @event.ElementId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling ElementCreated event for element {ElementId} in process {ProcessInstanceId}",
                @event.ElementId, @event.InstanceId);
            throw;
        }
    }

    /// <summary>
    /// Handles merge logic for gateway elements that join multiple incoming flows
    /// </summary>
    /// <returns>True if the gateway is in a waiting state, false if processing should continue</returns>
    private async Task<bool> HandleMergeGatewayIfNeededAsync(
        ElementCreated @event,
        EventHandlerContext context,
        DefiantionExplorer definitionExplorer,
        CancellationToken cancellationToken)
    {
        var execution = context.CurrentExecution;
        
        // Only handle gateway types that can act as merges
        if (!(@event.ElementType == BpmnElementType.ParallelGateway ||
              @event.ElementType == BpmnElementType.InclusiveGateway ||
              @event.ElementType == BpmnElementType.ExclusiveGateway))
        {
            return false; // Not a gateway that needs merge handling
        }
        
        Logger.LogDebug("Checking merge requirements for gateway {ElementId} of type {ElementType}",
            @event.ElementId, @event.ElementType);
        
        // Find all incoming flows for this gateway
        var incomingFlows = await FindIncomingFlowsAsync(
            definitionExplorer, 
            @event.ProcessId,
            @event.ElementId);
            
        // If there's only one incoming flow, no merge is needed
        if (incomingFlows.Count <= 1)
        {
            Logger.LogDebug("Gateway {ElementId} has only one incoming flow, no merge needed",
                @event.ElementId);
            return false;
        }
        
        // Get all ElementCreated events for this execution
        var receivedEvents = context.CurrentExecution.Events
            .OfType<SerializableBpmnEvent>()
            .Where(e => e.EventType == "ElementCreated" && !string.IsNullOrEmpty(e.SequenceFlowId))
            .ToList();
        
        // Count the unique sequence flow IDs we've received
        var receivedFlowIds = new HashSet<string>();
        foreach (var evt in receivedEvents)
        {
            if (!string.IsNullOrEmpty(evt.SequenceFlowId))
            {
                receivedFlowIds.Add(evt.SequenceFlowId);
            }
        }
        
        // Check if any of the incoming flows are executable
        // Since we can't directly access IsExecutable on SerializableBpmnEvent,
        // we'll use the current event's IsExecutable as a proxy
        bool hasExecutableFlow = @event.IsExecutable;
        
        // Log the current state
        Logger.LogDebug("Gateway {ElementId} received {ReceivedCount}/{TotalCount} flows, executable: {HasExecutable}", 
            @event.ElementId, receivedFlowIds.Count, incomingFlows.Count, hasExecutableFlow);
        
        bool canContinue = false;
        
        // Check if the merge requirements are satisfied based on gateway type
        if (@event.ElementType == BpmnElementType.ExclusiveGateway)
        {
            // Exclusive gateway always continues on the first activated flow
            canContinue = true;
            Logger.LogDebug("Exclusive gateway {ElementId} can continue with flow {FlowId}",
                @event.ElementId, @event.SequenceFlowId);
        }
        else if (@event.ElementType == BpmnElementType.ParallelGateway)
        {
            // Parallel gateway requires all incoming flows to be activated
            canContinue = receivedFlowIds.Count >= incomingFlows.Count;
            Logger.LogDebug("Parallel gateway {ElementId} activated flows: {Current}/{Required}, can continue: {CanContinue}",
                @event.ElementId, receivedFlowIds.Count, incomingFlows.Count, canContinue);
        }
        else if (@event.ElementType == BpmnElementType.InclusiveGateway)
        {
            // Inclusive gateway is more complex - needs to determine which flows are active
            // For simplicity, we'll use a similar approach to parallel gateway for now
            canContinue = receivedFlowIds.Count >= incomingFlows.Count;
            Logger.LogDebug("Inclusive gateway {ElementId} activated flows: {Current}/{Required}, can continue: {CanContinue}",
                @event.ElementId, receivedFlowIds.Count, incomingFlows.Count, canContinue);
        }
        
        if (!canContinue)
        {
            // Gateway needs to wait for more flows to be activated
            execution.Status = ExecutionStatus.Waiting;
            
            // Store the current state with the waiting execution
            await StateStore.UpsertAsync(context.State, ct: cancellationToken);
            
            Logger.LogInformation("Gateway {ElementId} of type {ElementType} is waiting for more flows ({Current}/{Required})",
                @event.ElementId, @event.ElementType, receivedFlowIds.Count, incomingFlows.Count);
                
            return true; // Indicate that processing should stop here
        }
        
        // Set the executable flag for the current execution based on received events
        // If any of the incoming flows are executable, this execution should be executable
        execution.IsExecutable = hasExecutableFlow;
        
        // If we get here, the gateway merge is complete and we can continue processing
        Logger.LogInformation("Gateway {ElementId} merge complete, continuing with processing as {ExecutableStatus}",
            @event.ElementId, execution.IsExecutable ? "executable" : "non-executable");
        
        // Update the current event's executable flag to match the execution
        if (@event is ElementCreated elementCreated)
        {
            // Using reflection since ElementCreated might be immutable
            try
            {
                var prop = elementCreated.GetType().GetProperty("IsExecutable");
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(elementCreated, execution.IsExecutable);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not update IsExecutable flag on event");
            }
        }
        
        // Store updated state
        await StateStore.UpsertAsync(context.State, ct: cancellationToken);
        
        return false; // Indicate that processing should continue
    }

    /// <summary>
    /// Finds all incoming sequence flows for a specific element
    /// </summary>
    private async Task<List<string>> FindIncomingFlowsAsync(
        DefiantionExplorer definitionExplorer,
        string processId,
        string elementId)
    {
       
        var incomingFlows = definitionExplorer.FindIncommingSequenceFlows(processId, elementId);
        
        return incomingFlows.Select(x => x.id).ToList();
    }

    /// <summary>
    /// Publishes the appropriate specialized processing event based on the element type
    /// </summary>
    private async Task PublishSpecializedProcessingEvent(
        ElementCreated @event,
        string executionId,
        DefiantionExplorer defiantionExplorer,
        CancellationToken cancellationToken)
    {
        // Common properties for all processing events
        var now = DateTimeOffset.UtcNow;
        var commonProps = new
        {
            ProcessInstanceId = @event.InstanceId,
            ProcessId = @event.ProcessId,
            DeploymentKey = @event.DeploymentKey,
            DeploymentId = @event.DeploymentId,
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            ExecutionId = executionId,
            Timestamp = now
        };


        // We need to use object.Equals for comparison since BpmnElementType is a class, not an enum
        // Determine which specialized event to publish based on element type

        if (Equals(@event.ElementType, BpmnElementType.UserTask))
        {

            var userTask = defiantionExplorer.FindUserTask(@event.ProcessId, @event.ElementId);

            PublishLater(new UserTaskProcessing
            {
                InstanceId = commonProps.ProcessInstanceId,
                DeploymentKey = commonProps.DeploymentKey,
                ProcessId = commonProps.ProcessId,
                DeploymentId = commonProps.DeploymentId,
                ElementId = commonProps.ElementId,
                ElementType = commonProps.ElementType,
                ExecutionId = commonProps.ExecutionId,
                Timestamp = commonProps.Timestamp,
                FormId = userTask?.formId,
                Assignee = userTask?.assignee,
                CandidateGroups = userTask?.candidateGroups,
                CandidateUsers = userTask?.candidateUsers,
            });
        }
        else if (Equals(@event.ElementType, BpmnElementType.ServiceTask))
        {
            var serviceTask = defiantionExplorer.FindServiceTask(@event.ProcessId, @event.ElementId);

            PublishLater(new ServiceTaskProcessing
            {
                ProcessId = commonProps.ProcessId,
                InstanceId = commonProps.ProcessInstanceId,
                DeploymentKey = commonProps.DeploymentKey,
                DeploymentId = commonProps.DeploymentId,
                ElementId = commonProps.ElementId,
                ElementType = commonProps.ElementType,
                ExecutionId = commonProps.ExecutionId,
                Timestamp = commonProps.Timestamp,
                Implementation = serviceTask?.implementation,
            });
        }
        else if (Equals(@event.ElementType, BpmnElementType.ScriptTask))
        {
            var scriptTask = defiantionExplorer.FindScriptTask(@event.ProcessId, @event.ElementId);

            PublishLater(new ScriptTaskProcessing
            {
                ProcessId = commonProps.ProcessId,
                InstanceId = commonProps.ProcessInstanceId,
                DeploymentKey = commonProps.DeploymentKey,
                DeploymentId = commonProps.DeploymentId,
                ElementId = commonProps.ElementId,
                ElementType = commonProps.ElementType,
                ExecutionId = commonProps.ExecutionId,
                Timestamp = commonProps.Timestamp,
                Script = scriptTask?.script.InnerText,
                ScriptFormat = scriptTask?.scriptFormat,
            });
        }
        else if (Equals(@event.ElementType, BpmnElementType.BusinessRuleTask))
        {
            var businessRuleTask = defiantionExplorer.FindBusinessRuleTask(@event.ProcessId, @event.ElementId);

            PublishLater(new BusinessRuleTaskProcessing
            {
                ProcessId = commonProps.ProcessId,
                InstanceId = commonProps.ProcessInstanceId,
                DeploymentKey = commonProps.DeploymentKey,
                DeploymentId = commonProps.DeploymentId,
                ElementId = commonProps.ElementId,
                ElementType = commonProps.ElementType,
                ExecutionId = commonProps.ExecutionId,
                Timestamp = commonProps.Timestamp,
                DecisionKey = businessRuleTask?.implementation,
            });
        }
        else if (Equals(@event.ElementType, BpmnElementType.ManualTask))
        {
            var manualTask = defiantionExplorer.FindManualTask(@event.ProcessId, @event.ElementId);

            PublishLater(new ManualTaskProcessing
            {
                ProcessId = commonProps.ProcessId,
                InstanceId = commonProps.ProcessInstanceId,
                DeploymentKey = commonProps.DeploymentKey,
                DeploymentId = commonProps.DeploymentId,
                ElementId = commonProps.ElementId,
                ElementType = commonProps.ElementType,
                ExecutionId = commonProps.ExecutionId,
                Timestamp = commonProps.Timestamp,
            });
        }
        else if (Equals(@event.ElementType, BpmnElementType.ReceiveTask))
        {
            var receiveTask = defiantionExplorer.FindReceiveTask(@event.ProcessId, @event.ElementId);
            PublishLater(new ReceivedTaskProcessing
            {
                InstanceId = commonProps.ProcessInstanceId,
                DeploymentKey = commonProps.DeploymentKey,
                DeploymentId = commonProps.DeploymentId,
                ProcessId = commonProps.ProcessId,
                ElementId = commonProps.ElementId,
                ElementType = commonProps.ElementType,
                ExecutionId = commonProps.ExecutionId,
                Timestamp = commonProps.Timestamp,
            });
        }
        else if (Equals(@event.ElementType, BpmnElementType.SendTask))
        {
            var sendTask = defiantionExplorer.FindSendTask(@event.ProcessId, @event.ElementId);
            PublishLater(new SendTaskProcessing
            {
                InstanceId = commonProps.ProcessInstanceId,
                DeploymentKey = commonProps.DeploymentKey,
                DeploymentId = commonProps.DeploymentId,
                ElementId = commonProps.ElementId,
                ProcessId = commonProps.ProcessId,
                ElementType = commonProps.ElementType,
                ExecutionId = commonProps.ExecutionId,
                Timestamp = commonProps.Timestamp,
            });
        }
        else if (Equals(@event.ElementType, BpmnElementType.Task))
        {
            PublishLater(new ElementProcessing
            {
                ProcessId = commonProps.ProcessId,
                InstanceId = commonProps.ProcessInstanceId,
                DeploymentKey = commonProps.DeploymentKey,
                DeploymentId = commonProps.DeploymentId,
                ElementId = commonProps.ElementId,
                ElementType = commonProps.ElementType,
                ExecutionId = commonProps.ExecutionId,
                Timestamp = commonProps.Timestamp,
            });
        }
        else
        {
            // For other types, publish the generic ElementProcessing event
            PublishLater(new ElementProcessing
            {
                ProcessId = commonProps.ProcessId,
                InstanceId = commonProps.ProcessInstanceId,
                DeploymentKey = commonProps.DeploymentKey,
                DeploymentId = commonProps.DeploymentId,
                ElementId = commonProps.ElementId,
                ElementType = commonProps.ElementType,
                ExecutionId = commonProps.ExecutionId,
                Timestamp = commonProps.Timestamp
            });
        }
        
        await Task.CompletedTask;
    }
}