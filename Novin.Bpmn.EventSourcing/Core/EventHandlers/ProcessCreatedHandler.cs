using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.Models;
using System.Collections.Generic;
using System.Linq;
using Novin.Bpmn.EventSourcing.Core.Models;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// Handles the creation of new process instances and activates start events
/// </summary>
public class ProcessCreatedHandler : BaseEventHandler<ProcessInstanceCreated>
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<ProcessCreatedHandler> _logger;

    public ProcessCreatedHandler(
        ILogger<ProcessCreatedHandler> logger,
        IStateStore stateStore,
        IEventStore eventStore,
        IDefinitionStore definitionStore,
        IEventBus eventBus)
        : base(stateStore, eventStore, definitionStore, logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    protected override async Task ProcessEventAsync(ProcessInstanceCreated @event, CancellationToken cancellationToken)
    {
        try
        {
            // Get process definition
            var definition = await DefinitionStore.GetParsedDefinitionAsync(
                @event.ProcessDefinitionKey,
                xml => ParseBpmnXml(xml),
                cancellationToken);

            if (definition == null)
            {
                throw new InvalidOperationException($"Process definition {@event.ProcessDefinitionKey} not found");
            }

            // Find process
            var process = FindProcess(definition, @event.ProcessDefinitionId);
            if (process == null)
            {
                throw new InvalidOperationException($"Process {@event.ProcessDefinitionId} not found in definition");
            }

            // Find start events
            var startEvents = FindStartEvents(process);
            if (!startEvents.Any())
            {
                throw new InvalidOperationException($"No start events found in process {@event.ProcessDefinitionId}");
            }

            // Create initial state
            var state = new BpmnProcessState
            {
                ProcessInstanceId = @event.ProcessInstanceId,
                ProcessDefinitionId = @event.ProcessDefinitionId,
                DeploymentKey = @event.ProcessDefinitionKey,
                DefinitionVersion = @event.ProcessDefinitionVersion,
                ActiveElements = new HashSet<string>(),
                CompletedElements = new HashSet<string>(),
                Variables = @event.Variables ?? new Dictionary<string, object>(),
                Status = ProcessStatus.Created,
                ExecutionPaths = new List<ExecutionPath>(),
                ActiveExecutions = new Dictionary<string, ExecutionPath>(),
                ElementExecutionPaths = new Dictionary<string, List<string>>(),
                ElementToSequenceFlows = new Dictionary<string, List<string>>(),
                ElementExecutionCounts = new Dictionary<string, int>(),
                GatewayMergeStates = new Dictionary<string, GatewayMergeInfo>(),
                EventToExecutionPath = new Dictionary<string, string>()
            };

            // Save initial state
            await SaveStateAsync(@event.ProcessInstanceId, state, null, cancellationToken);

            // Save event
            await SaveEventAsync(@event, cancellationToken);

            // Publish process started event
            await _eventBus.PublishAsync(new ProcessInstanceStarted
            {
                ProcessInstanceId = @event.ProcessInstanceId,
                ProcessDefinitionId = @event.ProcessDefinitionId,
                ProcessDefinitionKey = @event.ProcessDefinitionKey,
                ProcessDefinitionVersion = @event.ProcessDefinitionVersion,
                Intent = "STARTED",
                Timestamp = DateTime.UtcNow
            }, cancellationToken);

            _logger.LogInformation("Process instance {ProcessInstanceId} created and started", @event.ProcessInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling process instance creation for {ProcessInstanceId}", @event.ProcessInstanceId);
            throw;
        }
    }

    private BpmnDefinitions ParseBpmnXml(string xmlContent)
    {
        if (string.IsNullOrEmpty(xmlContent))
            throw new ArgumentException("XML content cannot be empty", nameof(xmlContent));

        try
        {
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(BpmnDefinitions));
            using var reader = new System.IO.StringReader(xmlContent);
            var definitions = (BpmnDefinitions)serializer.Deserialize(reader);

            if (definitions == null)
                throw new InvalidOperationException("Failed to deserialize BPMN XML");

            return definitions;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error parsing BPMN XML content");
            throw;
        }
    }

    private BpmnProcess FindProcess(BpmnDefinitions definitions, string processId)
    {
        if (definitions.Items == null || !definitions.Items.Any())
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

    private List<BpmnStartEvent> FindStartEvents(BpmnProcess process)
    {
        if (process?.Items == null || !process.Items.Any())
            return new List<BpmnStartEvent>();

        return process.Items
            .OfType<BpmnStartEvent>()
            .ToList();
    }
}