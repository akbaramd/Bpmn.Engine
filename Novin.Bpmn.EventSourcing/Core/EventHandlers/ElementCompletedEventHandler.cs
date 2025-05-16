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

        var isGateway = topology.Nodes.TryGetValue(@event.ElementId, out var node) && node.IsGateway;

        // مسیرهای خروجی از این المان
        if (!topology.Outgoing.TryGetValue(@event.ElementId, out var targetElementIds) || targetElementIds.Count == 0)
            return;

        // شاخه‌های معتبر با بررسی FEEL
        var validFlows = new List<(string TargetElementId, Dictionary<string, object?> Metadata)>();

        foreach (var targetId in targetElementIds)
        {
            var flow = topology.SequenceFlows.Values.FirstOrDefault(f =>
                f.SourceRef == @event.ElementId && f.TargetRef == targetId);

            if (flow == null) continue;

            bool conditionPass = true;

            if (!string.IsNullOrWhiteSpace(flow.ConditionExpression))
            {
                try
                {
                    var result = FeelEngine.Evaluate<bool>("="+flow.ConditionExpression, context.LocalVariables);
                    conditionPass = result;
                }
                catch
                {
                    conditionPass = false;
                }
            }

            if (conditionPass)
            {
                validFlows.Add((flow.TargetRef, flow.Metadata));
            }
        }

        // اگر Gateway باشد، باید کانتکست فعلی Completed شود و شاخه‌های جدید ساخته شوند
        if (isGateway)
        {
            context.State = ExecutionState.Completed;
            context.Version++;
            _contextRepository.Save(context);

            foreach (var (targetId, metadata) in validFlows)
            {
                var child = new ExecutionContext
                {
                    ContextId = Guid.NewGuid(),
                    InstanceId = context.InstanceId,
                    ParentContextId = context.ContextId,
                    CurrentElementId = targetId,
                    State = ExecutionState.Active,
                    LocalVariables = new Dictionary<string, object?>(context.LocalVariables),
                    Version = 0
                };

                foreach (var kv in metadata)
                    child.LocalVariables[kv.Key] = kv.Value;

                _contextRepository.Save(child);

                AppendEvent(new ElementCreated
                {
                    EventId = Guid.NewGuid(),
                    DeploymentId = @event.DeploymentId,
                    DeploymentKey = @event.DeploymentKey,
                    InstanceId = @event.InstanceId,
                    ProcessId = @event.ProcessId,
                    ElementId = targetId,
                    ExecutionId = child.ContextId,
                    ElementType = topology.Nodes[targetId].ElementType,
                    Timestamp = DateTime.UtcNow,
                    Version = 1,
                    IsExecutable = true
                });
            }
        }
        else
        {
            // در حالت غیر Gateway فقط کانتکست را Move می‌کنیم
            var next = validFlows.First(); // فرض بر این است فقط یکی مجاز است

            context.MoveToNext(next.TargetElementId);
            context.Version++;
            _contextRepository.Save(context);

            AppendEvent(new ElementCreated
            {
                EventId = Guid.NewGuid(),
                DeploymentId = @event.DeploymentId,
                DeploymentKey = @event.DeploymentKey,
                InstanceId = @event.InstanceId,
                ProcessId = @event.ProcessId,
                ElementId = next.TargetElementId,
                ExecutionId = context.ContextId,
                ElementType = topology.Nodes[next.TargetElementId].ElementType,
                Timestamp = DateTime.UtcNow,
                Version = 1,
                IsExecutable = true
            });
        }

        await Task.CompletedTask;
    }
}
