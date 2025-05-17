using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Topology;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

public class ElementCreatedEventHandler : BpmnEventHandlerBase<ElementCreated>
{
    private readonly IExecutionContextRepository _contextRepository;
    private readonly IFlowTopologyStore _topologyStore;
    private readonly IJoinResolverService _joinResolver;

    public ElementCreatedEventHandler(IServiceProvider serviceProvider,
                                      IExecutionContextRepository contextRepository,
                                      IFlowTopologyStore topologyStore,
                                      IJoinResolverService joinResolver)
        : base(serviceProvider)
    {
        _contextRepository = contextRepository ?? throw new ArgumentNullException(nameof(contextRepository));
        _topologyStore = topologyStore ?? throw new ArgumentNullException(nameof(topologyStore));
        _joinResolver = joinResolver ?? throw new ArgumentNullException(nameof(joinResolver));
    }

    public override async Task HandleAsync(ElementCreated @event, CancellationToken cancellationToken = default)
    {
        var context = _contextRepository.Get(@event.ExecutionId)
                      ?? throw new InvalidOperationException("Execution context not found.");

        var topology = _topologyStore.Get(@event.DeploymentId, @event.ProcessId)
                       ?? throw new InvalidOperationException("Flow topology not found.");

        if (!topology.Nodes.TryGetValue(@event.ElementId, out var targetNode))
            throw new InvalidOperationException($"Node not found for ElementId '{@event.ElementId}'.");

        if (!IsJoinNode(topology, @event.ElementId, targetNode))
        {
            AppendEvent(CreateProcessingEvent(@event, targetNode.ElementType, @event.IsExecutable));
            return;
        }

        // نود Join است
        var incomingIds = topology.Incoming[@event.ElementId];
        var candidateContexts = _contextRepository
            .GetByInstanceId(@event.InstanceId)
            .Where(c => incomingIds.Contains(c.PreviousElementId))
            .ToList();

        context.Merged();
        _contextRepository.Save(context);

        if (!_joinResolver.CanJoin(topology, @event.ElementId, candidateContexts))
        {
            // منتظر رسیدن بقیه شاخه‌ها بمان
            return;
        }

        // اگر همه شاخه‌ها رسیدند، ادامه بده
        var isExecutable = candidateContexts.Any(c => c.IsExecutable);
        AppendEvent(CreateProcessingEvent(@event, targetNode.ElementType, isExecutable));

        await Task.CompletedTask;
    }

    private static bool IsJoinNode(FlowTopology topology, string nodeId, FlowNode node)
    {
        return node.IsGateway && topology.Incoming.TryGetValue(nodeId, out var incoming) && incoming.Count > 1;
    }

    private static ElementProcessing CreateProcessingEvent(ElementCreated e, string elementType, bool isExecutable)
    {
        return new ElementProcessing
        {
            EventId = Guid.NewGuid(),
            ExecutionId = e.ExecutionId,
            InstanceId = e.InstanceId,
            DeploymentId = e.DeploymentId,
            DeploymentKey = e.DeploymentKey,
            ProcessId = e.ProcessId,
            ElementId = e.ElementId,
            ElementType = elementType,
            Timestamp = DateTime.UtcNow,
            IsExecutable = isExecutable
        };
    }
}
