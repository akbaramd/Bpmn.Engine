using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Topology;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.EventSourcing.Core.Services.Gateway;
using Novin.Bpmn.EventSourcing.Core.Join;
using Novin.Bpmn.EventSourcing.Core.Services.Variable;
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
    private readonly IGatewayBehaviorFactory _gatewayBehaviorFactory;
    private readonly IJoinStateStore _joinStateStore;
    private readonly IVariableMergeService _variableMergeService;

    public ElementCreatedEventHandler(IServiceProvider serviceProvider,
                                      IExecutionContextRepository contextRepository,
                                      IFlowTopologyStore topologyStore,
                                      IJoinResolverService joinResolver,
                                      IGatewayBehaviorFactory gatewayBehaviorFactory,
                                      IJoinStateStore joinStateStore,
                                      IVariableMergeService variableMergeService)
        : base(serviceProvider)
    {
        _contextRepository = contextRepository ?? throw new ArgumentNullException(nameof(contextRepository));
        _topologyStore = topologyStore ?? throw new ArgumentNullException(nameof(topologyStore));
        _joinResolver = joinResolver ?? throw new ArgumentNullException(nameof(joinResolver));
        _gatewayBehaviorFactory = gatewayBehaviorFactory ?? throw new ArgumentNullException(nameof(gatewayBehaviorFactory));
        _joinStateStore = joinStateStore ?? throw new ArgumentNullException(nameof(joinStateStore));
        _variableMergeService = variableMergeService ?? throw new ArgumentNullException(nameof(variableMergeService));
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
            AppendEvent(CreateProcessingEvent(@event, targetNode, @event.IsExecutable));
            return;
        }

        // نود Join است - استفاده از JoinState برای مدیریت race condition و idempotency
        var joinNodeId = @event.ElementId;
        var joinCycleId = 0; // TODO: محاسبه joinCycleId برای loopها
        
        // دریافت یا ایجاد JoinState
        var joinState = _joinStateStore.Get(@event.InstanceId, joinNodeId, joinCycleId);
        if (joinState == null)
        {
            // پیدا کردن incoming sequence flow IDs
            var incomingSequenceFlowIds = topology.SequenceFlows.Values
                .Where(f => f.TargetRef == joinNodeId)
                .Select(f => f.Id)
                .ToList();

            joinState = _joinStateStore.Create(
                @event.InstanceId,
                joinNodeId,
                incomingSequenceFlowIds,
                joinCycleId);
        }

        // بررسی اینکه آیا این context قبلاً consume شده است
        if (joinState.IsConsumed(context.ContextId))
        {
            // این context قبلاً در join استفاده شده است
            return;
        }

        // ثبت arrival token
        if (string.IsNullOrEmpty(context.LastSequenceFlowId))
        {
            // اگر LastSequenceFlowId مشخص نیست، از PreviousElementId استفاده کن
            // پیدا کردن sequence flow که از PreviousElementId به joinNodeId می‌رود
            var sequenceFlow = topology.SequenceFlows.Values
                .FirstOrDefault(f => f.SourceRef == context.PreviousElementId && f.TargetRef == joinNodeId);
            
            if (sequenceFlow == null)
            {
                throw new InvalidOperationException($"SequenceFlow not found from '{context.PreviousElementId}' to '{joinNodeId}'.");
            }
            
            context.LastSequenceFlowId = sequenceFlow.Id;
        }

        // ثبت arrival در JoinState (با retry برای optimistic concurrency)
        const int maxRetries = 5;
        bool registered = false;
        for (int i = 0; i < maxRetries; i++)
        {
            if (joinState.RegisterArrival(context.ContextId, context.LastSequenceFlowId))
            {
                if (_joinStateStore.Save(joinState))
                {
                    registered = true;
                    break;
                }
            }
            
            // Retry: دریافت مجدد JoinState
            joinState = _joinStateStore.Get(@event.InstanceId, joinNodeId, joinCycleId);
            if (joinState == null || joinState.Fired)
                return; // Join قبلاً fire شده
        }

        if (!registered)
        {
            throw new InvalidOperationException($"Failed to register arrival for join '{joinNodeId}' after {maxRetries} retries.");
        }

        // استفاده از GatewayBehavior برای بررسی CanJoin
        var gatewayBehavior = _gatewayBehaviorFactory.CreateBehavior(targetNode);
        var arrivedSequenceFlowIds = joinState.ArrivedTokens
            .Select(t => t.Split(':')[1])
            .Distinct()
            .ToList();

        var isInclusiveGateway = gatewayBehavior.GatewayType == "InclusiveGateway";
        var activeIncomingSequenceFlowIds = isInclusiveGateway ? joinState.ActiveIncomingSequenceFlowIds.ToList() : null;

        bool canJoin = gatewayBehavior.CanJoin(
            topology,
            targetNode,
            joinState.RequiredIncomingSequenceFlowIds,
            arrivedSequenceFlowIds,
            activeIncomingSequenceFlowIds);

        if (!canJoin)
        {
            // منتظر رسیدن بقیه شاخه‌ها بمان
            return;
        }

        // Fire کردن join (با retry برای optimistic concurrency)
        IReadOnlyList<Guid> consumedContextIds = Array.Empty<Guid>();
        for (int i = 0; i < maxRetries; i++)
        {
            if (joinState.CanFire(joinState.RequiredIncomingSequenceFlowIds, isInclusiveGateway))
            {
                consumedContextIds = joinState.Fire();
                if (_joinStateStore.Save(joinState))
                {
                    break;
                }
            }
            
            // Retry: دریافت مجدد JoinState
            joinState = _joinStateStore.Get(@event.InstanceId, joinNodeId, joinCycleId);
            if (joinState == null || joinState.Fired)
            {
                // Join قبلاً fire شده توسط handler دیگر
                return;
            }
        }

        // Consume کردن tokenهای ورودی
        foreach (var consumedContextId in consumedContextIds)
        {
            var consumedContext = _contextRepository.Get(consumedContextId);
            if (consumedContext != null)
            {
                consumedContext.UpdateState(ExecutionState.Completed);
                _contextRepository.Save(consumedContext);
            }
        }

        // دریافت contextهای مصرف شده
        var consumedContexts = consumedContextIds
            .Select(id => _contextRepository.Get(id))
            .Where(c => c != null)
            .Cast<ExecutionContext>()
            .ToList()
            .AsReadOnly();

        // Merge کردن متغیرها با استفاده از VariableMergeService
        var mergeStrategy = VariableMergeStrategy.LastWriteWins; // TODO: از configuration یا metadata Gateway بگیر
        var mergedVariables = _variableMergeService.MergeVariables(consumedContexts, mergeStrategy);

        // ایجاد context جدید برای ادامه اجرا (merge شده)
        var mergedContext = new ExecutionContext
        {
            ContextId = Guid.NewGuid(),
            InstanceId = @event.InstanceId,
            State = ExecutionState.Active,
            IsExecutable = true,
            LocalVariables = mergedVariables
        };

        // ارسال VariablesMerged event برای event-sourcing
        AppendEvent(new VariablesMerged
        {
            EventId = Guid.NewGuid(),
            InstanceId = @event.InstanceId,
            DeploymentId = @event.DeploymentId,
            DeploymentKey = @event.DeploymentKey,
            ProcessId = @event.ProcessId,
            MergedVariables = mergedVariables,
            MergedExecutionIds = consumedContextIds,
            NewExecutionId = mergedContext.ContextId,
            Strategy = mergeStrategy,
            Timestamp = DateTime.UtcNow
        });

        mergedContext.MoveToNext(joinNodeId);
        _contextRepository.Save(mergedContext);

        // اگر می‌توان join کرد، ادامه بده
        var isExecutable = consumedContextIds.Any(id => 
        {
            var ctx = _contextRepository.Get(id);
            return ctx?.IsExecutable ?? false;
        });

        AppendEvent(CreateProcessingEvent(@event, targetNode, isExecutable));

        await Task.CompletedTask;
    }

    private static bool IsJoinNode(FlowTopology topology, string nodeId, FlowNode node)
    {
        return node.IsGateway && topology.Incoming.TryGetValue(nodeId, out var incoming) && incoming.Count > 1;
    }

    private static ElementProcessing CreateProcessingEvent(ElementCreated e, FlowNode node, bool isExecutable)
    {
        var baseEvent = new
        {
            EventId = Guid.NewGuid(),
            ExecutionId = e.ExecutionId,
            InstanceId = e.InstanceId,
            DeploymentId = e.DeploymentId,
            DeploymentKey = e.DeploymentKey,
            ProcessId = e.ProcessId,
            ElementId = e.ElementId,
            ElementType = node.ElementType,
            Timestamp = DateTime.UtcNow,
            IsExecutable = isExecutable
        };

        // Create typed events based on element type and metadata
        if (node.ElementType.Contains("ScriptTask", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[CreateProcessingEvent] Creating ScriptTaskProcessing for ElementId: {e.ElementId}");
            Console.WriteLine($"[CreateProcessingEvent] Node Metadata keys: {string.Join(", ", node.Metadata.Keys)}");
            
            // Priority: BonyanScriptBody > Script (fallback)
            var script = node.Metadata.TryGetValue("BonyanScriptBody", out var bonyanScriptBodyValue) 
                ? bonyanScriptBodyValue?.ToString() ?? string.Empty
                : (node.Metadata.TryGetValue("Script", out var scriptValue) 
                    ? scriptValue?.ToString() ?? string.Empty 
                    : string.Empty);
            
            var scriptFormat = node.Metadata.TryGetValue("BonyanScriptFormat", out var bonyanFormat)
                ? bonyanFormat?.ToString()
                : (node.Metadata.TryGetValue("ScriptLanguage", out var formatValue)
                    ? formatValue?.ToString()
                    : null);

            Console.WriteLine($"[CreateProcessingEvent] Extracted Script: '{script}', ScriptFormat: {scriptFormat}");
            var bonyanScriptBodyDebug = node.Metadata.TryGetValue("BonyanScriptBody", out var bs) ? bs?.ToString() : "NOT FOUND";
            var scriptFallback = node.Metadata.TryGetValue("Script", out var s) ? s?.ToString() : "NOT FOUND";
            Console.WriteLine($"[CreateProcessingEvent] BonyanScriptBody: {bonyanScriptBodyDebug}");
            Console.WriteLine($"[CreateProcessingEvent] Script (fallback): {scriptFallback}");

            return new ScriptTaskProcessing
            {
                EventId = baseEvent.EventId,
                ExecutionId = baseEvent.ExecutionId,
                InstanceId = baseEvent.InstanceId,
                DeploymentId = baseEvent.DeploymentId,
                DeploymentKey = baseEvent.DeploymentKey,
                ProcessId = baseEvent.ProcessId,
                ElementId = baseEvent.ElementId,
                ElementType = baseEvent.ElementType,
                Timestamp = baseEvent.Timestamp,
                IsExecutable = baseEvent.IsExecutable,
                Script = script,
                ScriptFormat = scriptFormat
            };
        }
        else if (node.ElementType.Contains("ServiceTask", StringComparison.OrdinalIgnoreCase))
        {
            var implementation = node.Metadata.TryGetValue("Implementation", out var implValue)
                ? implValue?.ToString()
                : null;

            return new ServiceTaskProcessing
            {
                EventId = baseEvent.EventId,
                ExecutionId = baseEvent.ExecutionId,
                InstanceId = baseEvent.InstanceId,
                DeploymentId = baseEvent.DeploymentId,
                DeploymentKey = baseEvent.DeploymentKey,
                ProcessId = baseEvent.ProcessId,
                ElementId = baseEvent.ElementId,
                ElementType = baseEvent.ElementType,
                Timestamp = baseEvent.Timestamp,
                IsExecutable = baseEvent.IsExecutable,
                Implementation = implementation
            };
        }
        else if (node.ElementType.Contains("UserTask", StringComparison.OrdinalIgnoreCase))
        {
            // For UserTask, we'd need formId and other properties from metadata
            // For now, create a basic UserTaskProcessing
            return new UserTaskProcessing
            {
                EventId = baseEvent.EventId,
                ExecutionId = baseEvent.ExecutionId,
                InstanceId = baseEvent.InstanceId,
                DeploymentId = baseEvent.DeploymentId,
                DeploymentKey = baseEvent.DeploymentKey,
                ProcessId = baseEvent.ProcessId,
                ElementId = baseEvent.ElementId,
                ElementType = baseEvent.ElementType,
                Timestamp = baseEvent.Timestamp,
                IsExecutable = baseEvent.IsExecutable,
                FormId = node.Metadata.TryGetValue("FormId", out var formValue) 
                    ? formValue?.ToString() ?? e.ElementId 
                    : e.ElementId
            };
        }

        // Default: generic ElementProcessing
        return new ElementProcessing
        {
            EventId = baseEvent.EventId,
            ExecutionId = baseEvent.ExecutionId,
            InstanceId = baseEvent.InstanceId,
            DeploymentId = baseEvent.DeploymentId,
            DeploymentKey = baseEvent.DeploymentKey,
            ProcessId = baseEvent.ProcessId,
            ElementId = baseEvent.ElementId,
            ElementType = baseEvent.ElementType,
            Timestamp = baseEvent.Timestamp,
            IsExecutable = baseEvent.IsExecutable
        };
    }
}
