using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Topology;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.EventSourcing.Core.Process;
using Novin.Bpmn.EventSourcing.Core.Services;
using Novin.Bpmn.EventSourcing.Feel;
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
    private readonly IProcessStateStore _processStateStore;
    private readonly IForkHandlerService _forkHandler;

    public ElementCompletedEventHandler(IServiceProvider serviceProvider,
                                        IExecutionContextRepository contextRepository,
                                        IFlowTopologyStore topologyStore,
                                        IProcessStateStore processStateStore,
                                        IForkHandlerService forkHandler)
        : base(serviceProvider)
    {
        _contextRepository = contextRepository ?? throw new ArgumentNullException(nameof(contextRepository));
        _topologyStore = topologyStore ?? throw new ArgumentNullException(nameof(topologyStore));
        _processStateStore = processStateStore ?? throw new ArgumentNullException(nameof(processStateStore));
        _forkHandler = forkHandler ?? throw new ArgumentNullException(nameof(forkHandler));
    }

    public override async Task HandleAsync(ElementCompleted @event, CancellationToken cancellationToken = default)
    {
        var context = _contextRepository.Get(@event.ExecutionId)
                      ?? throw new InvalidOperationException($"ExecutionContext not found for Id {@event.ExecutionId}");

        var topology = _topologyStore.Get(@event.DeploymentId, @event.ProcessId)
                       ?? throw new InvalidOperationException("Topology not found");

        var processState = _processStateStore.Get(context.InstanceId)
                           ?? throw new InvalidOperationException($"ProcessState not found for InstanceId {context.InstanceId}");

        SyncLocalVariablesToProcessState(context, processState);

        if (IsEndEvent(@event.ElementId, topology))
        {
            FinalizeProcess(context, processState, @event);
            return;
        }

        if (!topology.Outgoing.TryGetValue(@event.ElementId, out var outgoingTargets) || outgoingTargets.Count == 0)
            return;

        var currentNode = topology.Nodes[@event.ElementId];

        if (currentNode.IsGateway)
        {
            context.State = ExecutionState.Completed;
            _contextRepository.Save(context);

            var forks = _forkHandler.PrepareForks(context, @event, topology, outgoingTargets);
            foreach (var fork in forks)
            {
                _contextRepository.Save(fork);
                var nextNode = topology.Nodes[fork.CurrentElementId!];

                AppendEvent(CreateElementCreatedEvent(@event, fork.CurrentElementId!, fork.ContextId, nextNode.ElementType, fork.IsExecutable));
            }
        }
        else
        {
            HandleSequentialFlow(context, @event, topology, outgoingTargets);
        }

        await Task.CompletedTask;
    }

    private void SyncLocalVariablesToProcessState(ExecutionContext context, ProcessState processState)
    {
        foreach (var kv in context.LocalVariables)
            processState.Variables[kv.Key] = kv.Value;
    }

    private void FinalizeProcess(ExecutionContext context, ProcessState processState, ElementCompleted @event)
    {
        context.State = ExecutionState.Completed;
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
            Timestamp = DateTime.UtcNow
        });
    }

    private void HandleSequentialFlow(ExecutionContext context, ElementCompleted @event, FlowTopology topology, List<string> targets)
    {
        foreach (var targetId in targets)
        {
            var flow = topology.SequenceFlows.Values
                .FirstOrDefault(f => f.SourceRef == @event.ElementId && f.TargetRef == targetId);
            if (flow == null) continue;

            if (!IsSequenceConditionSatisfied(flow.ConditionExpression, context.LocalVariables))
                continue;

            context.MoveToNext(targetId);

            foreach (var kv in flow.Metadata)
                context.LocalVariables[kv.Key] = kv.Value;

            context.State = topology.Nodes.TryGetValue(targetId, out var nextNode) && nextNode.IsGateway
                ? ExecutionState.Completed
                : ExecutionState.Active;

            _contextRepository.Save(context);

            AppendEvent(CreateElementCreatedEvent(@event, targetId, context.ContextId, nextNode.ElementType, context.IsExecutable));
        }
    }

    private bool IsSequenceConditionSatisfied(string? expression, Dictionary<string, object?> variables)
    {
        if (string.IsNullOrWhiteSpace(expression)) return true;

        try
        {
            return FeelEngine.Evaluate<bool>(expression, variables);
        }
        catch
        {
            return false;
        }
    }

    private ElementCreated CreateElementCreatedEvent(ElementCompleted source, string elementId, Guid executionId, string elementType, bool isExecutable)
    {
        return new ElementCreated
        {
            EventId = Guid.NewGuid(),
            DeploymentId = source.DeploymentId,
            DeploymentKey = source.DeploymentKey,
            InstanceId = source.InstanceId,
            ProcessId = source.ProcessId,
            ElementId = elementId,
            ExecutionId = executionId,
            ElementType = elementType,
            Timestamp = DateTime.UtcNow,
            Version = 1,
            IsExecutable = isExecutable
        };
    }

    private bool IsEndEvent(string elementId, FlowTopology topology)
    {
        return topology.Nodes.TryGetValue(elementId, out var node) &&
               node.ElementType.Equals(BpmnElementType.EndEvent.NameWithNamespace, StringComparison.OrdinalIgnoreCase);
    }
}
