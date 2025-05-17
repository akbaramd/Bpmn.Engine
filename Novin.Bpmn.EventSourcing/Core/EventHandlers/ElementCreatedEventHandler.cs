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
        
        //get current context 
        var currentContext = _contextRepository.Get(@event.ExecutionId)
                             ?? throw new InvalidOperationException("Context not found.");

        if (!currentContext.IsExecutable || !@event.IsExecutable)
        {
            AppendEvent(new ElementCompleted()
            {
                EventId = Guid.NewGuid(),
                ExecutionId = @event.ExecutionId,
                InstanceId = @event.InstanceId,
                DeploymentId = @event.DeploymentId,
                DeploymentKey = @event.DeploymentKey,
                ProcessId = @event.ProcessId,
                ElementId = @event.ElementId,
                ElementType = @event.ElementType,
                Timestamp = DateTime.UtcNow,
                IsExecutable = false
            });
            return;
        }
        
        
        var topology = _topologyStore.Get(@event.DeploymentId, @event.ProcessId)
                       ?? throw new InvalidOperationException("Topology not found.");

        if (!topology.Nodes.TryGetValue(@event.ElementId, out var targetNode))
            throw new InvalidOperationException($"Node not found for ElementId '{@event.ElementId}'.");

        // بررسی اینکه آیا نود Join است یا خیر
        bool isJoinNode = targetNode.IsGateway && 
                          topology.Incoming.TryGetValue(@event.ElementId, out var incomingFlows) ;

        if (!isJoinNode)
        {
            var elementProcessingEvent = new ElementProcessing()
            {
                EventId = Guid.NewGuid(),
                ExecutionId = @event.ExecutionId,
                InstanceId = @event.InstanceId,
                DeploymentId = @event.DeploymentId,
                DeploymentKey = @event.DeploymentKey,
                ProcessId = @event.ProcessId,
                ElementId = @event.ElementId,
                ElementType = targetNode.ElementType,
                Timestamp = DateTime.UtcNow,
                IsExecutable = true
            };

            AppendEvent(elementProcessingEvent);
            return;
        }

        // اگر نود Join هست، کانتکست‌های ورودی رو بگیر
        var candidateContexts = _contextRepository.GetByInstanceId(@event.InstanceId)
            .Where(c => topology.Incoming[@event.ElementId].Contains(c.PreviousElementId))
            .ToList();

        // بررسی آیا همه شاخه‌ها رسیدن
        bool canJoin = _joinResolver.CanJoin(topology, @event.ElementId, candidateContexts);

        if (!canJoin)
        {
            // همه شاخه‌ها نرسیدن، منتظر بمان
            return;
        }

        
         var elementProcessingEvent2 = new ElementProcessing()
        {
            EventId = Guid.NewGuid(),
            ExecutionId = @event.ExecutionId,
            InstanceId = @event.InstanceId,
            DeploymentId = @event.DeploymentId,
            DeploymentKey = @event.DeploymentKey,
            ProcessId = @event.ProcessId,
            ElementId = @event.ElementId,
            ElementType = targetNode.ElementType,
            Timestamp = DateTime.UtcNow,
            IsExecutable = candidateContexts.Any(x=>x.IsExecutable)
        };

        AppendEvent(elementProcessingEvent2);
        // حذف کانتکست‌های قبلی شاخه‌ه
        await Task.CompletedTask;
    }

}
