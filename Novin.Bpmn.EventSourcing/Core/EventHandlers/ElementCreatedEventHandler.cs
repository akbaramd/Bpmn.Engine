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

        var isJoinNode = topology.Nodes.TryGetValue(ev.ElementId.ToString(), out var node) && node.IsJoinNode;

        var currentContext = _contextRepository.Get(ev.ExecutionId);

        if (!isJoinNode)
        {
            if (currentContext != null)
            {
                currentContext.CurrentElementId = ev.ElementId.ToString();
                currentContext.State            = ExecutionState.Active;
                currentContext.Version++;
                _contextRepository.Save(currentContext);

                await PublishElementProcessingEvent(ev, currentContext.ContextId);
            }
            return;
        }

        // Join Node:
        var allContexts = _contextRepository.GetByInstanceId(ev.InstanceId)
            .Where(c => c.ParentContextId != null)
            .ToList();

        if (!_joinResolverService.CanJoin(topology, ev.ElementId.ToString(), allContexts))
        {
            AppendEvent(ev); // Retry later
            return;
        }

        var relevantContexts = allContexts
            .Where(c => c.Path != null && topology.Incoming[ev.ElementId.ToString()].Contains(c.Path.Last()))
            .ToList();

        var mergedContext = _joinResolverService.MergeContexts(topology, ev.ElementId.ToString(), relevantContexts);

        foreach (var ctx in relevantContexts)
            _contextRepository.Remove(ctx.ContextId);

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
