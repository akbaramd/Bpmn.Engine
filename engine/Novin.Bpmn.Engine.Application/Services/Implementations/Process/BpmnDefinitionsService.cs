using Microsoft.Extensions.Logging;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Service for managing and querying BPMN Definitions
/// </summary>
public class BpmnDefinitionsService
{
    private readonly BpmnDefinitions _definitions;
    private readonly ILogger<BpmnDefinitionsService>? _logger;

    public BpmnDefinitionsService(BpmnDefinitions definitions, ILogger<BpmnDefinitionsService>? logger = null)
    {
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        _logger = logger;
    }

    /// <summary>
    /// Gets the first process from definitions
    /// </summary>
    public BpmnProcess GetFirstProcess()
    {
        var process = _definitions.Items?.OfType<BpmnProcess>().FirstOrDefault();
        
        if (process == null)
            throw new InvalidOperationException("No process found in BPMN definitions.");
        
        return process;
    }
    public bool EvaluateCondition(string condition, Dictionary<string, string> variables)
    {
        // This method evaluates a condition (in FEEL or other supported expression language)
        // The simplest way would be to use something like C# Expression API or an external FEEL evaluator library
        var expr = new ExpressionEvaluator();
        return expr.Evaluate(condition, variables);
    }
    /// <summary>
    /// Gets a process by ID
    /// </summary>
    public BpmnProcess GetProcess(string processId)
    {
        var process = _definitions.Items?.OfType<BpmnProcess>()
            .FirstOrDefault(p => p.id == processId);
        
        if (process == null)
            throw new InvalidOperationException($"Process with ID '{processId}' not found in BPMN definitions.");
        
        return process;
    }

    /// <summary>
    /// Gets all start events for a process
    /// </summary>
    public List<BpmnStartEvent> GetStartEvents(string processId)
    {
        var process = GetProcess(processId);
        return process.Items?.OfType<BpmnStartEvent>().ToList() ?? new List<BpmnStartEvent>();
    }

    /// <summary>
    /// Gets the first start event for a process
    /// </summary>
    public BpmnStartEvent? GetFirstStartEvent(string processId)
    {
        return GetStartEvents(processId).FirstOrDefault();
    }

    /// <summary>
    /// Gets all end events for a process
    /// </summary>
    public List<BpmnEndEvent> GetEndEvents(string processId)
    {
        var process = GetProcess(processId);
        return process.Items?.OfType<BpmnEndEvent>().ToList() ?? new List<BpmnEndEvent>();
    }

    /// <summary>
    /// Gets a flow element by ID
    /// </summary>
    public BpmnFlowElement? GetElementById(string processId, string elementId)
    {
        var process = GetProcess(processId);
        return process.Items?.OfType<BpmnFlowElement>()
            .FirstOrDefault(e => e.id == elementId);
    }

    /// <summary>
    /// Gets all outgoing sequence flows from an element
    /// </summary>
    public List<BpmnSequenceFlow> GetOutgoingSequenceFlows(string processId, string elementId)
    {
        var process = GetProcess(processId);
        var sequenceFlows = process.Items?.OfType<BpmnSequenceFlow>().ToList() ?? new List<BpmnSequenceFlow>();
        
        return sequenceFlows
            .Where(sf => sf.sourceRef == elementId)
            .ToList();
    }

    /// <summary>
    /// Gets all incoming sequence flows to an element
    /// </summary>
    public List<BpmnSequenceFlow> GetIncomingSequenceFlows(string processId, string elementId)
    {
        var process = GetProcess(processId);
        var sequenceFlows = process.Items?.OfType<BpmnSequenceFlow>().ToList() ?? new List<BpmnSequenceFlow>();
        
        return sequenceFlows
            .Where(sf => sf.targetRef == elementId)
            .ToList();
    }

    /// <summary>
    /// Gets the next elements (targets) from an element
    /// </summary>
    public List<BpmnFlowElement> GetNextElements(string processId, string elementId, Dictionary<string, string> variables)
    {
        var process = GetProcess(processId);
        var element = process.Items?.OfType<BpmnFlowElement>().FirstOrDefault(e => e.id == elementId);

        if (element == null)
            throw new InvalidOperationException($"Element with id '{elementId}' not found in the process.");

        var outgoingFlows = GetOutgoingSequenceFlows(processId, elementId);
        var nextElements = new List<BpmnFlowElement>();

        foreach (var flow in outgoingFlows)
        {
            if (EvaluateCondition(flow.conditionExpression.Text[0], variables)) // Check condition
            {
                nextElements.Add(GetElement(flow.targetRef));
            }
        }

        return nextElements;
    }
    public BpmnFlowElement GetElement(string elementId)
    {
        return _definitions.Items?.OfType<BpmnFlowElement>().FirstOrDefault(e => e.id == elementId);
    }
    
    /// <summary>
    /// Gets the previous elements (sources) of an element
    /// </summary>
    public List<BpmnFlowElement> GetPreviousElements(string processId, string elementId)
    {
        var incomingFlows = GetIncomingSequenceFlows(processId, elementId);
        var process = GetProcess(processId);
        var allElements = process.Items?.OfType<BpmnFlowElement>().ToList() ?? new List<BpmnFlowElement>();

        var previousElements = new List<BpmnFlowElement>();
        
        foreach (var flow in incomingFlows)
        {
            if (!string.IsNullOrEmpty(flow.sourceRef))
            {
                var sourceElement = allElements.FirstOrDefault(e => e.id == flow.sourceRef);
                if (sourceElement != null)
                {
                    previousElements.Add(sourceElement);
                }
            }
        }

        return previousElements;
    }

    /// <summary>
    /// Gets all tasks in a process
    /// </summary>
    public List<BpmnTask> GetTasks(string processId)
    {
        var process = GetProcess(processId);
        return process.Items?.OfType<BpmnTask>().ToList() ?? new List<BpmnTask>();
    }

    /// <summary>
    /// Gets all user tasks in a process
    /// </summary>
    public List<BpmnUserTask> GetUserTasks(string processId)
    {
        var process = GetProcess(processId);
        return process.Items?.OfType<BpmnUserTask>().ToList() ?? new List<BpmnUserTask>();
    }

    /// <summary>
    /// Gets all service tasks in a process
    /// </summary>
    public List<BpmnServiceTask> GetServiceTasks(string processId)
    {
        var process = GetProcess(processId);
        return process.Items?.OfType<BpmnServiceTask>().ToList() ?? new List<BpmnServiceTask>();
    }

    /// <summary>
    /// Gets all gateways in a process
    /// </summary>
    public List<BpmnGateway> GetGateways(string processId)
    {
        var process = GetProcess(processId);
        return process.Items?.OfType<BpmnGateway>().ToList() ?? new List<BpmnGateway>();
    }

    /// <summary>
    /// Gets all exclusive gateways in a process
    /// </summary>
    public List<BpmnExclusiveGateway> GetExclusiveGateways(string processId)
    {
        var process = GetProcess(processId);
        return process.Items?.OfType<BpmnExclusiveGateway>().ToList() ?? new List<BpmnExclusiveGateway>();
    }

    /// <summary>
    /// Gets all parallel gateways in a process
    /// </summary>
    public List<BpmnParallelGateway> GetParallelGateways(string processId)
    {
        var process = GetProcess(processId);
        return process.Items?.OfType<BpmnParallelGateway>().ToList() ?? new List<BpmnParallelGateway>();
    }

    /// <summary>
    /// Gets all intermediate catch events in a process
    /// </summary>
    public List<BpmnIntermediateCatchEvent> GetIntermediateCatchEvents(string processId)
    {
        var process = GetProcess(processId);
        return process.Items?.OfType<BpmnIntermediateCatchEvent>().ToList() ?? new List<BpmnIntermediateCatchEvent>();
    }

    /// <summary>
    /// Gets all boundary events attached to an element
    /// </summary>
    public List<BpmnBoundaryEvent> GetBoundaryEvents(string processId, string attachedToRef)
    {
        var process = GetProcess(processId);
        
        // Debug: Log all items in process to see what types exist
        var allItems = process.Items ?? Array.Empty<BpmnFlowElement>();
        _logger?.LogDebug(
            "[BPMN-DEFS] GetBoundaryEvents: ProcessId={ProcessId} AttachedToRef={AttachedToRef} TotalItems={TotalItems}",
            processId,
            attachedToRef,
            allItems.Length);
        
        // Log item types for debugging
        if (allItems.Length > 0)
        {
            var itemTypes = allItems
                .Where(i => i != null)
                .GroupBy(i => i!.GetType().Name)
                .Select(g => $"{g.Key}({g.Count()})");
            _logger?.LogDebug(
                "[BPMN-DEFS] Process items breakdown: {ItemTypes}",
                string.Join(", ", itemTypes));
        }
        
        // Get all boundary events from process.Items
        var allBoundaryEvents = allItems
            .Where(i => i != null)
            .OfType<BpmnBoundaryEvent>()
            .ToList();
        
        _logger?.LogDebug(
            "[BPMN-DEFS] GetBoundaryEvents: ProcessId={ProcessId} AttachedToRef={AttachedToRef} TotalBoundaryEvents={TotalCount}",
            processId,
            attachedToRef,
            allBoundaryEvents.Count);
        
        // Debug: Log all boundary events found
        foreach (var be in allBoundaryEvents)
        {
            _logger?.LogDebug(
                "[BPMN-DEFS] BoundaryEvent found: Id={BoundaryEventId} AttachedToRef={AttachedToRef} LookingFor={LookingFor} Match={Match}",
                be.id,
                be.attachedToRef?.Name ?? "NULL",
                attachedToRef,
                be.attachedToRef?.Name == attachedToRef);
        }
        
        // Filter by attachedToRef
        var matchingBoundaryEvents = allBoundaryEvents
            .Where(be => be.attachedToRef != null && be.attachedToRef.Name == attachedToRef)
            .ToList();
        
        _logger?.LogDebug(
            "[BPMN-DEFS] GetBoundaryEvents result: ProcessId={ProcessId} AttachedToRef={AttachedToRef} MatchingCount={MatchingCount}",
            processId,
            attachedToRef,
            matchingBoundaryEvents.Count);
        
        return matchingBoundaryEvents;
    }

    /// <summary>
    /// Checks if an element is a start event
    /// </summary>
    public bool IsStartEvent(string processId, string elementId)
    {
        var startEvents = GetStartEvents(processId);
        return startEvents.Any(se => se.id == elementId);
    }

    /// <summary>
    /// Checks if an element is an end event
    /// </summary>
    public bool IsEndEvent(string processId, string elementId)
    {
        var endEvents = GetEndEvents(processId);
        return endEvents.Any(ee => ee.id == elementId);
    }

    /// <summary>
    /// Checks if an element is a gateway
    /// </summary>
    public bool IsGateway(string processId, string elementId)
    {
        var gateways = GetGateways(processId);
        return gateways.Any(g => g.id == elementId);
    }

    /// <summary>
    /// Gets all flow elements in a process
    /// </summary>
    public List<BpmnFlowElement> GetAllFlowElements(string processId)
    {
        var process = GetProcess(processId);
        return process.Items?.OfType<BpmnFlowElement>().ToList() ?? new List<BpmnFlowElement>();
    }

    /// <summary>
    /// Gets an error element from BPMN definitions by its ID.
    /// Error elements are stored in the root Items array of BpmnDefinitions.
    /// </summary>
    public BpmnError? GetErrorElement(string errorElementId)
    {
        if (string.IsNullOrWhiteSpace(errorElementId))
            return null;

        return _definitions.Items?
            .OfType<BpmnError>()
            .FirstOrDefault(e => e.id == errorElementId);
    }

    public List<BpmnFlowElement> GetFlowElements(string ctxBpmnProcessId)
    {
        var process = GetProcess(ctxBpmnProcessId);
        return process.Items?.OfType<BpmnFlowElement>().ToList() ?? new List<BpmnFlowElement>();
    }

    public List<BpmnSequenceFlow> GetSequenceFlows(string ctxBpmnProcessId)
    {
        var process = GetProcess(ctxBpmnProcessId);
        var sequenceFlows = process.Items?.OfType<BpmnSequenceFlow>().ToList() ?? new List<BpmnSequenceFlow>();
        
        return sequenceFlows
            .ToList();
    }
}

