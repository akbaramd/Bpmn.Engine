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

        var startNodes = topology.Nodes.Values.Where(n => n.IsStartEvent);

        foreach (var startNode in startNodes)
        {
            var context = new ExecutionContext
            {
                IsExecutable = true,
                ContextId = Guid.NewGuid(),
                InstanceId = @event.InstanceId,
                LocalVariables = state.Variables,
                State = ExecutionState.Active,
            };

            context.MoveToNext(startNode.ElementId);

            _contextRepository.Save(context);

            var elementCreatedEvent = new ElementCreated()
            {
                ExecutionId = context.ContextId,
                EventId = Guid.NewGuid(),
                InstanceId = @event.InstanceId,
                DeploymentKey = @event.DeploymentKey,
                DeploymentId = @event.DeploymentId,
                ProcessId = @event.ProcessId,
                ElementId = startNode.ElementId,
                ElementType = startNode.ElementType,
                Timestamp = DateTime.UtcNow
            };

            AppendEvent(elementCreatedEvent);
        }

        await Task.CompletedTask;
    }
}