    using Microsoft.Extensions.Logging;
    using Novin.Bpmn.EventSourcing.Core.Executions;
    using Novin.Bpmn.EventSourcing.Core.Process;
    using Novin.Bpmn.EventSourcing.Events;
    using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

public class ProcessStartedEventHandler : BpmnEventHandlerBase<ProcessStarted>
{
    private readonly IFlowTopologyStore _topologyStore;
    private readonly IExecutionContextRepository _contextRepository;
    private readonly IProcessStateStore _processStateStore;
    private readonly ILogger<ProcessStartedEventHandler> _logger;

    public ProcessStartedEventHandler(IServiceProvider serviceProvider, 
                                      IFlowTopologyStore topologyStore,
                                      IExecutionContextRepository contextRepository,
                                      IProcessStateStore processStateStore,
                                      ILogger<ProcessStartedEventHandler> logger)
        : base(serviceProvider)
    {
        _topologyStore = topologyStore ?? throw new ArgumentNullException(nameof(topologyStore));
        _contextRepository = contextRepository ?? throw new ArgumentNullException(nameof(contextRepository));
        _processStateStore = processStateStore ?? throw new ArgumentNullException(nameof(processStateStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task HandleAsync(ProcessStarted @event, CancellationToken cancellationToken = default)
    {
        var topology = _topologyStore.Get(@event.DeploymentId, @event.ProcessId);
        if (topology == null)
        {
            _logger.LogError("Topology not found for DeploymentId: {DeploymentId}, ProcessId: {ProcessId}", @event.DeploymentId, @event.ProcessId);
            throw new InvalidOperationException("Topology not found");
        }

        // ایجاد یا بروزرسانی ProcessState با وضعیت Active
        var state = _processStateStore.Get(@event.InstanceId);
        if (state == null)
        {
            state = new ProcessState
            {
                InstanceId = @event.InstanceId,
                DeploymentKey = @event.DeploymentKey,
                DeploymentId = @event.DeploymentId,
                ProcessId = @event.ProcessId,
                Variables = @event.InitializeVariables ?? new Dictionary<string, object?>(),
                Status = ProcessStateStatus.Active,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                Version = 1
            };
            _processStateStore.Save(state);
        }
        else
        {
            state.Status = ProcessStateStatus.Active;
            state.LastUpdatedAt = DateTime.UtcNow;
            _processStateStore.Save(state);
        }

        // پیدا کردن StartEvent که باید trigger شود
        FlowNode? targetStartNode = null;

        if (!string.IsNullOrEmpty(@event.StartEventId))
        {
            // اگر StartEventId مشخص شده، همان را پیدا کن
            if (topology.Nodes.TryGetValue(@event.StartEventId, out var specifiedNode) && specifiedNode.IsStartEvent)
            {
                targetStartNode = specifiedNode;
            }
            else
            {
                _logger.LogWarning("Specified StartEventId '{StartEventId}' not found or is not a StartEvent. Falling back to None StartEvent.", @event.StartEventId);
            }
        }

        // اگر StartEventId مشخص نشده یا پیدا نشد، None StartEvent را پیدا کن
        if (targetStartNode == null)
        {
            // اول None StartEvent را جستجو کن
            targetStartNode = topology.Nodes.Values
                .FirstOrDefault(n => n.IsStartEvent && 
                    (n.StartEventType == "None" || 
                     n.ElementType.Contains("noneStartEvent", StringComparison.OrdinalIgnoreCase) ||
                     string.IsNullOrEmpty(n.StartEventType)));

            // اگر None StartEvent پیدا نشد، اولین StartEvent را بگیر
            if (targetStartNode == null)
            {
                targetStartNode = topology.Nodes.Values.FirstOrDefault(n => n.IsStartEvent);
            }
        }

        if (targetStartNode == null)
        {
            _logger.LogError("No StartEvent found in process {ProcessId}", @event.ProcessId);
            throw new InvalidOperationException($"No StartEvent found in process '{@event.ProcessId}'.");
        }

        // فقط یک ExecutionContext برای StartEvent مشخص شده بساز
        var context = new ExecutionContext
        {
            IsExecutable = true,
            ContextId = Guid.NewGuid(),
            InstanceId = @event.InstanceId,
            LocalVariables = new Dictionary<string, object?>(state.Variables),
            State = ExecutionState.Active,
        };

        context.MoveToNext(targetStartNode.ElementId);

        _contextRepository.Save(context);

        var elementCreatedEvent = new ElementCreated()
        {
            ExecutionId = context.ContextId,
            EventId = Guid.NewGuid(),
            InstanceId = @event.InstanceId,
            DeploymentKey = @event.DeploymentKey,
            DeploymentId = @event.DeploymentId,
            ProcessId = @event.ProcessId,
            ElementId = targetStartNode.ElementId,
            ElementType = targetStartNode.ElementType,
            Timestamp = DateTime.UtcNow
        };

        AppendEvent(elementCreatedEvent);

        await Task.CompletedTask;
    }
}