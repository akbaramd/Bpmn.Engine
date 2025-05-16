    using Novin.Bpmn.EventSourcing.Core.Executions;
    using Novin.Bpmn.EventSourcing.Events;
    using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

    public class ProcessStartedEventHandler : BpmnEventHandlerBase<ProcessStarted>
    {
        private readonly IFlowTopologyStore _topologyStore;
        private readonly IExecutionContextRepository _contextRepository;

        public ProcessStartedEventHandler(IServiceProvider serviceProvider, 
                                          IFlowTopologyStore topologyStore,
                                          IExecutionContextRepository contextRepository)
            : base(serviceProvider)
        {
            _topologyStore = topologyStore ?? throw new ArgumentNullException(nameof(topologyStore));
            _contextRepository = contextRepository ?? throw new ArgumentNullException(nameof(contextRepository));
        }

        public override async Task HandleAsync(ProcessStarted @event, CancellationToken cancellationToken = default)
        {
            var topology = _topologyStore.Get(@event.DeploymentId, @event.ProcessId);
            if (topology == null)
                throw new InvalidOperationException("Topology not found");

            var startNodes = topology.Nodes.Values.Where(n => n.IsStartEvent);

            foreach (var startNode in startNodes)
            {
                
                // 2. ساخت ExecutionContext مرتبط با این Element
                var context = new ExecutionContext
                {
                    ContextId = Guid.NewGuid(),
                    InstanceId = @event.InstanceId,
                    CurrentElementId = startNode.ElementId,
                    LocalVariables = @event.InitializeVariables,
                    Path = [startNode.ElementId],
                    State = ExecutionState.Active,
                    Version = 1,
                };

                // 3. ذخیره کانتکست
                _contextRepository.Save(context);
                
                // 1. تولید رویداد ElementCreated
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
