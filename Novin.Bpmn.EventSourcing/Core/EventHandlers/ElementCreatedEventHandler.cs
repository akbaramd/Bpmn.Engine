using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.EventSourcing.Core.Topology;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

public class ElementCreatedEventHandler : BpmnEventHandlerBase<ElementCreated>
{
    private readonly IExecutionContextRepository _contextRepository;
    private readonly IJoinResolverService        _joinResolverService;
    private readonly IFlowTopologyStore          _topologyStore;

    public ElementCreatedEventHandler(IServiceProvider serviceProvider,
                                      IExecutionContextRepository contextRepository,
                                      IJoinResolverService joinResolverService,
                                      IFlowTopologyStore topologyStore)
        : base(serviceProvider)
    {
        _contextRepository   = contextRepository ?? throw new ArgumentNullException(nameof(contextRepository));
        _joinResolverService = joinResolverService ?? throw new ArgumentNullException(nameof(joinResolverService));
        _topologyStore       = topologyStore ?? throw new ArgumentNullException(nameof(topologyStore));
    }

    public override async Task HandleAsync(ElementCreated ev, CancellationToken cancellationToken = default)
    {
        var topology = _topologyStore.Get(ev.DeploymentId, ev.ProcessId)
                       ?? throw new InvalidOperationException("Topology not found");

        var currentCtx = _contextRepository.Get(ev.ExecutionId);

        var isJoinNode = topology.Nodes.TryGetValue(ev.ElementId, out var node) && node.IsJoinNode;

        if (!isJoinNode)
        {
            if (currentCtx != null)
            {
                currentCtx.State = ExecutionState.Active;
                currentCtx.Version++;
                _contextRepository.Save(currentCtx);

                await PublishElementProcessingEvent(ev, currentCtx.ContextId);
            }
            return;
        }

        // Join Node:
        var incomingBranches = topology.Incoming.TryGetValue(ev.ElementId, out var incomingIds)
            ? incomingIds : new List<string>();

        var candidateContexts = _contextRepository.GetByInstanceId(ev.InstanceId);
            candidateContexts = candidateContexts
            .Where(c => c.Path.Count > 0 &&
                        incomingIds.Contains(c.Path.Last()) && // شاخه ورودی واقعی
                        c.State == ExecutionState.Completed)
            .ToList();

        if (!_joinResolverService.CanJoin(topology, ev.ElementId, candidateContexts))
        {
            AppendEvent(ev); // منتظر تکمیل شاخه‌های دیگر
            return;
        }

        var mergedContext = _joinResolverService.MergeContexts(topology, ev.ElementId,currentCtx, candidateContexts);

        _contextRepository.Save(mergedContext);

        await PublishElementProcessingEvent(ev, mergedContext.ContextId);
    }


    private Task PublishElementProcessingEvent(ElementCreated ev, Guid contextId)
    {
        AppendEvent(new ElementProcessing
        {
            EventId       = Guid.NewGuid(),
            InstanceId    = ev.InstanceId,
            DeploymentId  = ev.DeploymentId,
            DeploymentKey = ev.DeploymentKey,
            ProcessId     = ev.ProcessId,
            ElementId     = ev.ElementId,
            ExecutionId   = contextId,
            Timestamp     = DateTime.UtcNow,
            ElementType   = ev.ElementType,
            Version       = 1,
            IsExecutable  = true
        });

        return Task.CompletedTask;
    }
}
