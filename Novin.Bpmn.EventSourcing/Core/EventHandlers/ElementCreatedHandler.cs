using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Collections.Generic;
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
        : base(stateStore, eventStore, definitionStore, logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    /// <inheritdoc />
    protected override async Task ProcessEventAsync(
        ElementCreated @event,
        ProcessInstanceState state,
        CancellationToken cancellationToken)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        Logger.LogInformation("Handling ElementCreated event for process instance {ProcessInstanceId}, element {ElementId}",
            @event.ProcessInstanceId, @event.ElementId);

        try
        {
            // Record the event in state history
            state.RecordEvent(@event);

            // 1. Generate a unique execution ID
            var executionId = Guid.NewGuid().ToString();
            
            // 2. Build the ElementExecution and add to state
            var execution = ElementExecutionBuilder.Init()
                .WithProcessInstanceId(@event.ProcessInstanceId)
                .WithElementId(@event.ElementId)
                .WithElementType(@event.ElementType)
                .Executable(@event.IsExecutable)
                .Build()
                .BuildResult();

            state.AddExecution(execution);
            
            // 3. Handle based on element type and whether it's executable
            if (@event.IsExecutable)
            {
                // For executable elements, determine handling based on element type
                Logger.LogDebug("Processing executable element {ElementId} of type {ElementType}", 
                    @event.ElementId, @event.ElementType);
                
                // Create and publish the appropriate processing event based on element type
                await PublishSpecializedProcessingEvent(
                    @event, 
                    execution.ExecutionId, 
                    GetElementProperties(state, @event.ElementId), 
                    cancellationToken);
            }
            else
            {
                // For non-executable elements, complete immediately
                Logger.LogDebug("Element {ElementId} is non-executable, completing immediately", @event.ElementId);
                
                // Mark execution as complete in the state
                execution.Complete();
                
                // Publish completion event
                await _eventBus.PublishAsync(new ElementCompleted
                {
                    ProcessInstanceId = @event.ProcessInstanceId,
                    ProcessDefinitionId = @event.ProcessDefinitionId,
                    ElementId = @event.ElementId,
                    ElementType = @event.ElementType,
                    ExecutionId = execution.ExecutionId,
                    IsExecutable = false,
                    Timestamp = DateTimeOffset.UtcNow
                }, cancellationToken);
            }
            
            Logger.LogDebug("Successfully processed ElementCreated event for {ElementId}", @event.ElementId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling ElementCreated event for element {ElementId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            throw;
        }
    }

    /// <summary>
    /// Gets the element properties from the state or definition
    /// </summary>
    private Dictionary<string, string> GetElementProperties(ProcessInstanceState state, string elementId)
    {
        // In a real implementation, we would retrieve properties from the process definition
        // or from the state if it stores element metadata
        var properties = new Dictionary<string, string>();
        
        // Try to get properties from the state's variables
        if (state.Variables.TryGetValue($"element.{elementId}.properties", out var propsObj) &&
            propsObj is Dictionary<string, string> props)
        {
            return props;
        }
        
        // Default properties for testing/development
        return properties;
    }

    /// <summary>
    /// Publishes the appropriate specialized processing event based on the element type
    /// </summary>
    private async Task PublishSpecializedProcessingEvent(
        ElementCreated @event, 
        string executionId, 
        Dictionary<string, string> properties, 
        CancellationToken cancellationToken)
    {
        // Common properties for all processing events
        var now = DateTimeOffset.UtcNow;
        var commonProps = new
        {
            ProcessInstanceId = @event.ProcessInstanceId,
            ProcessDefinitionId = @event.ProcessDefinitionId,
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            ExecutionId = executionId,
            Timestamp = now
        };

        // Helper function to get property with default
        string GetProperty(string key, string defaultValue) =>
            properties.TryGetValue(key, out var value) ? value : defaultValue;

        // We need to use object.Equals for comparison since BpmnElementType is a class, not an enum
        // Determine which specialized event to publish based on element type
        
        if (Equals(@event.ElementType, BpmnElementType.UserTask))
        {
            await _eventBus.PublishAsync(new UserTaskProcessing
            {
                ProcessInstanceId = commonProps.ProcessInstanceId,
                ProcessDefinitionId = commonProps.ProcessDefinitionId,
                ElementId = commonProps.ElementId,
                ElementType = commonProps.ElementType,
                ExecutionId = commonProps.ExecutionId,
                Timestamp = commonProps.Timestamp,
                FormId = GetProperty("formKey", "default-form"),
                Assignee = GetProperty("assignee", null)
            }, cancellationToken);
        }
        else if (Equals(@event.ElementType, BpmnElementType.ServiceTask))
        {
            await _eventBus.PublishAsync(new ServiceTaskProcessing
            {
                ProcessInstanceId = commonProps.ProcessInstanceId,
                ProcessDefinitionId = commonProps.ProcessDefinitionId,
                ElementId = commonProps.ElementId,
                ElementType = commonProps.ElementType,
                ExecutionId = commonProps.ExecutionId,
                Timestamp = commonProps.Timestamp,
                ServiceName = GetProperty("serviceName", @event.ElementId),
                Endpoint = GetProperty("endpoint", null)
            }, cancellationToken);
        }
        else if (Equals(@event.ElementType, BpmnElementType.ScriptTask))
        {
            await _eventBus.PublishAsync(new ScriptTaskProcessing
            {
                ProcessInstanceId = commonProps.ProcessInstanceId,
                ProcessDefinitionId = commonProps.ProcessDefinitionId,
                ElementId = commonProps.ElementId,
                ElementType = commonProps.ElementType,
                ExecutionId = commonProps.ExecutionId,
                Timestamp = commonProps.Timestamp,
                Script = GetProperty("script", ""),
                ScriptFormat = GetProperty("scriptFormat", null)
            }, cancellationToken);
        }
        else if (Equals(@event.ElementType, BpmnElementType.BusinessRuleTask))
        {
            await _eventBus.PublishAsync(new BusinessRuleTaskProcessing
            {
                ProcessInstanceId = commonProps.ProcessInstanceId,
                ProcessDefinitionId = commonProps.ProcessDefinitionId,
                ElementId = commonProps.ElementId,
                ElementType = commonProps.ElementType,
                ExecutionId = commonProps.ExecutionId,
                Timestamp = commonProps.Timestamp,
                DecisionKey = GetProperty("decisionRef", @event.ElementId)
            }, cancellationToken);
        }
        else if (Equals(@event.ElementType, BpmnElementType.ManualTask))
        {
            await _eventBus.PublishAsync(new ManualTaskProcessing
            {
                ProcessInstanceId = commonProps.ProcessInstanceId,
                ProcessDefinitionId = commonProps.ProcessDefinitionId,
                ElementId = commonProps.ElementId,
                ElementType = commonProps.ElementType,
                ExecutionId = commonProps.ExecutionId,
                Timestamp = commonProps.Timestamp,
                Instruction = GetProperty("instruction", null)
            }, cancellationToken);
        }
        else if (Equals(@event.ElementType, BpmnElementType.ReceiveTask))
        {
            await _eventBus.PublishAsync(new ReceiveTaskProcessing
            {
                ProcessInstanceId = commonProps.ProcessInstanceId,
                ProcessDefinitionId = commonProps.ProcessDefinitionId,
                ElementId = commonProps.ElementId,
                ElementType = commonProps.ElementType,
                ExecutionId = commonProps.ExecutionId,
                Timestamp = commonProps.Timestamp,
                MessageName = GetProperty("messageName", @event.ElementId)
            }, cancellationToken);
        }
        else if (Equals(@event.ElementType, BpmnElementType.SendTask))
        {
            await _eventBus.PublishAsync(new SendTaskProcessing
            {
                ProcessInstanceId = commonProps.ProcessInstanceId,
                ProcessDefinitionId = commonProps.ProcessDefinitionId,
                ElementId = commonProps.ElementId,
                ElementType = commonProps.ElementType,
                ExecutionId = commonProps.ExecutionId,
                Timestamp = commonProps.Timestamp,
                MessageName = GetProperty("messageName", @event.ElementId),
                Payload = GetProperty("payload", null)
            }, cancellationToken);
        }
        else if (Equals(@event.ElementType, BpmnElementType.Task))
        {
            await _eventBus.PublishAsync(new TaskProcessing
            {
                ProcessInstanceId = commonProps.ProcessInstanceId,
                ProcessDefinitionId = commonProps.ProcessDefinitionId,
                ElementId = commonProps.ElementId,
                ElementType = commonProps.ElementType,
                ExecutionId = commonProps.ExecutionId,
                Timestamp = commonProps.Timestamp,
                Implementation = GetProperty("implementation", null)
            }, cancellationToken);
        }
        else
        {
            // For other types, publish the generic ElementProcessing event
            await _eventBus.PublishAsync(new ElementProcessing
            {
                ProcessInstanceId = commonProps.ProcessInstanceId,
                ProcessDefinitionId = commonProps.ProcessDefinitionId,
                ElementId = commonProps.ElementId,
                ElementType = commonProps.ElementType,
                ExecutionId = commonProps.ExecutionId,
                Timestamp = commonProps.Timestamp
            }, cancellationToken);
        }
    }
} 