using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.EventSourcing.Feel;
using Novin.Bpmn.EventSourcing.Core.Topology;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

public class ElementCompletedEventHandler : BpmnEventHandlerBase<ElementCompleted>
{
    private readonly IExecutionContextRepository _contextRepository;
    private readonly IFlowTopologyStore _topologyStore;

    public ElementCompletedEventHandler(IServiceProvider serviceProvider,
                                        IExecutionContextRepository contextRepository,
                                        IFlowTopologyStore topologyStore)
        : base(serviceProvider)
    {
        _contextRepository = contextRepository ?? throw new ArgumentNullException(nameof(contextRepository));
        _topologyStore = topologyStore ?? throw new ArgumentNullException(nameof(topologyStore));
    }

    public override async Task HandleAsync(ElementCompleted @event, CancellationToken cancellationToken = default)
    {
        var context = _contextRepository.Get(@event.ExecutionId)
                      ?? throw new InvalidOperationException($"ExecutionContext not found for Id {@event.ExecutionId}");

        var topology = _topologyStore.Get(@event.DeploymentId, @event.ProcessId)
                      ?? throw new InvalidOperationException("Topology not found");

        // اگر المان جاری EndEvent است
        if (topology.Nodes.TryGetValue(@event.ElementId, out var currentNode) && 
            currentNode.ElementType.ToLower() == BpmnElementType.EndEvent.NameWithNamespace.ToLower())
        {
            context.State = ExecutionState.Completed;
            context.Version++;
            _contextRepository.Save(context);

            // انتشار رویداد ProcessCompleted
            AppendEvent(new ProcessCompleted
            {
                EventId = Guid.NewGuid(),
                InstanceId = context.InstanceId,
                DeploymentId = @event.DeploymentId,
                DeploymentKey = @event.DeploymentKey,
                ProcessId = @event.ProcessId,
                Timestamp = DateTime.UtcNow,
            });

            return; // دیگر ادامه مسیر وجود ندارد
        }

        if (!topology.Outgoing.TryGetValue(@event.ElementId, out var targetIds) || targetIds.Count == 0)
            return;

        var isCurrentGateway = currentNode.IsGateway;

        foreach (var targetId in targetIds)
        {
            var sequenceFlow = topology.SequenceFlows.Values
                .FirstOrDefault(f => f.SourceRef == @event.ElementId && f.TargetRef == targetId);

            if (sequenceFlow == null)
                continue;

            bool conditionOk = true;
            if (!string.IsNullOrWhiteSpace(sequenceFlow.ConditionExpression))
            {
                try
                {
                    conditionOk = FeelEngine.Evaluate<bool>(sequenceFlow.ConditionExpression, context.LocalVariables);
                }
                catch
                {
                    conditionOk = false;
                }
            }
            if (!conditionOk)
                continue;

            var isNextGateway = topology.Nodes.TryGetValue(targetId, out var nextNode) && nextNode.IsGateway;

            if (isCurrentGateway)
            {
                context.MoveToNext(targetId);
                foreach (var kv in sequenceFlow.Metadata)
                    context.LocalVariables[kv.Key] = kv.Value;
                context.Version++;
                _contextRepository.Save(context);

                AppendEvent(new ElementCreated
                {
                    EventId = Guid.NewGuid(),
                    DeploymentId = @event.DeploymentId,
                    DeploymentKey = @event.DeploymentKey,
                    InstanceId = @event.InstanceId,
                    ProcessId = @event.ProcessId,
                    ElementId = targetId,
                    ExecutionId = context.ContextId,
                    ElementType = nextNode.ElementType,
                    Timestamp = DateTime.UtcNow,
                    Version = 1,
                    IsExecutable = true
                });
            }
            else
            {
                if (isNextGateway)
                {
                    context.State = ExecutionState.Completed;
                    context.Version++;
                    _contextRepository.Save(context);

                    var fork = new ExecutionContext
                    {
                        ContextId = Guid.NewGuid(),
                        InstanceId = context.InstanceId,
                        ParentContextId = context.ContextId,
                        State = ExecutionState.Active,
                        Version = 0,
                        LocalVariables = new Dictionary<string, object?>(context.LocalVariables)
                    };
                    fork.MoveToNext(targetId);

                    foreach (var kv in sequenceFlow.Metadata)
                        fork.LocalVariables[kv.Key] = kv.Value;

                    _contextRepository.Save(fork);

                    AppendEvent(new ElementCreated
                    {
                        EventId = Guid.NewGuid(),
                        DeploymentId = @event.DeploymentId,
                        DeploymentKey = @event.DeploymentKey,
                        InstanceId = @event.InstanceId,
                        ProcessId = @event.ProcessId,
                        ElementId = targetId,
                        ExecutionId = fork.ContextId,
                        ElementType = nextNode.ElementType,
                        Timestamp = DateTime.UtcNow,
                        Version = 1,
                        IsExecutable = true
                    });
                }
                else
                {
                    context.MoveToNext(targetId);
                    foreach (var kv in sequenceFlow.Metadata)
                        context.LocalVariables[kv.Key] = kv.Value;
                    context.Version++;
                    _contextRepository.Save(context);

                    AppendEvent(new ElementCreated
                    {
                        EventId = Guid.NewGuid(),
                        DeploymentId = @event.DeploymentId,
                        DeploymentKey = @event.DeploymentKey,
                        InstanceId = @event.InstanceId,
                        ProcessId = @event.ProcessId,
                        ElementId = targetId,
                        ExecutionId = context.ContextId,
                        ElementType = nextNode.ElementType,
                        Timestamp = DateTime.UtcNow,
                        Version = 1,
                        IsExecutable = true
                    });
                }
            }
        }

        await Task.CompletedTask;
    }
}
