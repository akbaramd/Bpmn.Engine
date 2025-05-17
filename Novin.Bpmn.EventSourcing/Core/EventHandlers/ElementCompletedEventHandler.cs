using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Topology;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.EventSourcing.Core.Process;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.EventSourcing.Feel;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

public class ElementCompletedEventHandler : BpmnEventHandlerBase<ElementCompleted>
{
    private readonly IExecutionContextRepository _contextRepository;
    private readonly IFlowTopologyStore _topologyStore;
    private readonly IProcessStateStore _processStateStore;

    public ElementCompletedEventHandler(IServiceProvider serviceProvider,
                                        IExecutionContextRepository contextRepository,
                                        IFlowTopologyStore topologyStore, IProcessStateStore processStateStore)
        : base(serviceProvider)
    {
        _contextRepository = contextRepository ?? throw new ArgumentNullException(nameof(contextRepository));
        _topologyStore = topologyStore ?? throw new ArgumentNullException(nameof(topologyStore));
        _processStateStore = processStateStore;
    }

    public override async Task HandleAsync(ElementCompleted @event, CancellationToken cancellationToken = default)
    {
        var context = _contextRepository.Get(@event.ExecutionId)
                      ?? throw new InvalidOperationException($"ExecutionContext not found for Id {@event.ExecutionId}");

        var topology = _topologyStore.Get(@event.DeploymentId, @event.ProcessId)
                      ?? throw new InvalidOperationException("Topology not found");

        var processState = _processStateStore.Get(context.InstanceId)
                           ?? throw new InvalidOperationException($"ProcessState not found for InstanceId {context.InstanceId}");

        // همگام‌سازی متغیرها
        foreach (var kv in context.LocalVariables)
            processState.Variables[kv.Key] = kv.Value;

        // اگر EndEvent است، وضعیت را کامل کن و ProcessCompleted بفرست
        if (topology.Nodes.TryGetValue(@event.ElementId, out var currentNode) &&
            currentNode.ElementType.Equals(BpmnElementType.EndEvent.NameWithNamespace, StringComparison.OrdinalIgnoreCase))
        {
            
            ontext.State = ExecutionState.Completed;
            _contextRepository.Save(context);

            processState.Status = ProcessStateStatus.Completed;
            processState.LastUpdatedAt = DateTime.UtcNow;
            processState.Version++;
            _processStateStore.Save(processState);

            AppendEvent(new ProcessCompleted
            {
                EventId = Guid.NewGuid(),
                InstanceId = context.InstanceId,
                DeploymentId = @event.DeploymentId,
                DeploymentKey = @event.DeploymentKey,
                ProcessId = @event.ProcessId,
                Timestamp = DateTime.UtcNow,
            });

            return;
        }

        if (!topology.Outgoing.TryGetValue(@event.ElementId, out var outgoingTargets) || outgoingTargets.Count == 0)
            return;

        var isCurrentGateway = currentNode.IsGateway;

        if (isCurrentGateway )
        {
            // Fork: کانتکست جاری رو کامل کن
            context.State = ExecutionState.Completed;
            _contextRepository.Save(context);

            foreach (var targetId in outgoingTargets)
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

                if (conditionOk)
                {
                    var fork = context.Clone();
                    fork.MoveToNext(targetId);
                    

                    foreach (var kv in sequenceFlow.Metadata)
                        fork.LocalVariables[kv.Key] = kv.Value;

                    _contextRepository.Save(fork);

                    var nextNode = topology.Nodes[targetId];

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
                        IsExecutable = @event.IsExecutable
                    });
                }
                else
                {
                    var fork = context.Clone();
                    fork.MoveToNext(targetId);
                    fork.State = ExecutionState.DeActive;
                    fork.IsExecutable = false;
                    _contextRepository.Save(fork);

                    var nextNode = topology.Nodes[targetId];

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
                        IsExecutable = @event.IsExecutable
                    });
                }
                    

               
            }
        }
        else
        {
            // مسیر ساده: ادامه با همان کانتکست
            foreach (var targetId in outgoingTargets)
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

                context.MoveToNext(targetId);

                foreach (var kv in sequenceFlow.Metadata)
                    context.LocalVariables[kv.Key] = kv.Value;

                // اگر نود بعدی یک Gateway باشد، وضعیت را به Completed تغییر بده
                if (topology.Nodes.TryGetValue(targetId, out var nextNode) && nextNode.IsGateway)
                {
                    context.State = ExecutionState.Completed;
                }
                else
                {
                    context.State = ExecutionState.Active;
                }
                
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
                    IsExecutable = @event.IsExecutable
                });
            }
        }

        await Task.CompletedTask;
    }
}
