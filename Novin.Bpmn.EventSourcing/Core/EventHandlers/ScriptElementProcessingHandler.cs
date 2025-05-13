using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// Handles the processing of BPMN elements and tasks
/// </summary>
public class ScriptElementProcessingHandler : BaseEventHandler<ScriptTaskProcessing>
{
    private readonly IEventBus _eventBus;
    private readonly ScriptExecuter _scriptExecuter;
    /// <summary>
    /// Creates a new instance of ElementProcessingHandler
    /// </summary>
    public ScriptElementProcessingHandler(
        IProcessInstanceStateStore stateStore,
        IEventStore eventStore,
        IProcessDeploymentStore definitionStore,
        IEventBus eventBus,
        ILogger<ScriptElementProcessingHandler> logger)
        : base(stateStore, eventStore, definitionStore, eventBus, logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _scriptExecuter = new ScriptExecuter();
    }

    /// <inheritdoc />
    protected override async Task ProcessEventAsync(
        ScriptTaskProcessing @event,
        EventHandlerContext context,
        CancellationToken cancellationToken)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        try
        {

            await _scriptExecuter.Execute(@event.Script, context.Execution);


            PublishLater(new ElementCompleted
            {
                InstanceId = @event.InstanceId,
                ProcessId = @event.ProcessId,
                DeploymentId = @event.DeploymentId,
                DeploymentKey = @event.DeploymentKey,
                ElementId = @event.ElementId,
                ElementType = @event.ElementType,
                ExecutionId = @event.ExecutionId,
                Timestamp = DateTimeOffset.UtcNow

            });

        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing element {ElementId} in process {ProcessInstanceId}",
                @event.ElementId, @event.InstanceId);

            // Create failed event 
            PublishLater(new ElementFailed
            {
                InstanceId = @event.InstanceId,
                ProcessId = @event.ProcessId,
                DeploymentId = @event.DeploymentId,
                DeploymentKey = @event.DeploymentKey,
                ElementId = @event.ElementId,
                ElementType = @event.ElementType,
                ExecutionId = @event.ExecutionId,
                ErrorCode = "PROCESSING_ERROR",
                ErrorMessage = ex.Message,
                Timestamp = DateTimeOffset.UtcNow
            });

            throw;
        }
    }

    /// <summary>
    /// Handle the element processing based on element type
    /// </summary>
    private async Task HandleElementTypeAsync(
        ElementProcessing @event,
        EventHandlerContext context,
        CancellationToken cancellationToken)
    {
        // Log the element type being processed
        Logger.LogDebug("Element {@ElementId} of type {@ElementType} processing handled",
            @event.ElementId, @event.ElementType);

        // Create a completed event
        PublishLater(new ElementCompleted
        {
            InstanceId = @event.InstanceId,
            ProcessId = @event.ProcessId,
            DeploymentId = @event.DeploymentId,
            DeploymentKey = @event.DeploymentKey,
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            ExecutionId = @event.ExecutionId,
            Timestamp = DateTimeOffset.UtcNow
        });
    }
}
