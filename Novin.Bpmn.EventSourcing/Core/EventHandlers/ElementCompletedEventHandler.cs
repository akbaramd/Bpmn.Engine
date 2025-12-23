using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Topology;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.EventSourcing.Core.Process;
using Novin.Bpmn.EventSourcing.Core.Services;
using Novin.Bpmn.EventSourcing.Core.Services.Gateway;
using Novin.Bpmn.Models.Models;
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
    private readonly IGatewayBehaviorFactory _gatewayBehaviorFactory;
    private readonly IoMappingApplier _ioMappingApplier;

    public ElementCompletedEventHandler(IServiceProvider serviceProvider,
                                        IExecutionContextRepository contextRepository,
                                        IFlowTopologyStore topologyStore, 
                                        IProcessStateStore processStateStore,
                                        IGatewayBehaviorFactory gatewayBehaviorFactory)
        : base(serviceProvider)
    {
        _contextRepository = contextRepository ?? throw new ArgumentNullException(nameof(contextRepository));
        _topologyStore = topologyStore ?? throw new ArgumentNullException(nameof(topologyStore));
        _processStateStore = processStateStore;
        _gatewayBehaviorFactory = gatewayBehaviorFactory ?? throw new ArgumentNullException(nameof(gatewayBehaviorFactory));
        _ioMappingApplier = new IoMappingApplier();
    }

    public override async Task HandleAsync(ElementCompleted @event, CancellationToken cancellationToken = default)
    {
        var context = _contextRepository.Get(@event.ExecutionId)
                      ?? throw new InvalidOperationException($"ExecutionContext not found for Id {@event.ExecutionId}");

        var topology = _topologyStore.Get(@event.DeploymentId, @event.ProcessId)
                      ?? throw new InvalidOperationException("Topology not found");

        var processState = _processStateStore.Get(context.InstanceId)
                           ?? throw new InvalidOperationException($"ProcessState not found for InstanceId {context.InstanceId}");

        // Apply output mappings: Node Variables → Process Variables (before syncing)
        ApplyOutputMappings(@event, context, processState);

        // همگام‌سازی متغیرها با event-sourcing (به جای overwrite مستقیم)
        // فقط متغیرهایی که تغییر کرده‌اند را event می‌فرستیم
        Console.WriteLine($"[ElementCompleted] Syncing variables for ElementId: {@event.ElementId}, ExecutionId: {context.ContextId}");
        Console.WriteLine($"[ElementCompleted] ExecutionContext LocalVariables: {string.Join(", ", context.LocalVariables.Select(kv => $"{kv.Key}={kv.Value}"))}");
        Console.WriteLine($"[ElementCompleted] ProcessState Variables before sync: {string.Join(", ", processState.Variables.Select(kv => $"{kv.Key}={kv.Value}"))}");
        
        if (context.LocalVariables != null && context.LocalVariables.Count > 0)
        {
            // بررسی تغییرات: فقط متغیرهایی که مقدارشان تغییر کرده یا جدید هستند
            var changedVariables = new Dictionary<string, object?>();
            foreach (var kv in context.LocalVariables)
            {
                if (!processState.Variables.TryGetValue(kv.Key, out var existingValue) ||
                    !Equals(existingValue, kv.Value))
                {
                    changedVariables[kv.Key] = kv.Value;
                    Console.WriteLine($"[ElementCompleted] Variable '{kv.Key}' changed: {existingValue} -> {kv.Value}");
                }
                else
                {
                    Console.WriteLine($"[ElementCompleted] Variable '{kv.Key}' unchanged: {kv.Value}");
                }
            }

            if (changedVariables.Count > 0)
            {
                Console.WriteLine($"[ElementCompleted] Publishing VariablesSet event with {changedVariables.Count} changed variables: {string.Join(", ", changedVariables.Select(kv => $"{kv.Key}={kv.Value}"))}");
                
                // ارسال VariablesSet event برای event-sourcing
                AppendEvent(new VariablesSet
                {
                    EventId = Guid.NewGuid(),
                    InstanceId = context.InstanceId,
                    DeploymentId = @event.DeploymentId,
                    DeploymentKey = @event.DeploymentKey,
                    ProcessId = @event.ProcessId,
                    Variables = changedVariables,
                    ExecutionId = context.ContextId,
                    Scope = VariableScope.Process,
                    Timestamp = DateTime.UtcNow
                });
            }
            else
            {
                Console.WriteLine($"[ElementCompleted] No variable changes detected - skipping VariablesSet event");
            }
        }
        else
        {
            Console.WriteLine($"[ElementCompleted] No LocalVariables to sync (null or empty)");
        }

        // اگر EndEvent است
        if (topology.Nodes.TryGetValue(@event.ElementId, out var currentNode) && currentNode.IsEndEvent)
        {
            // بررسی نوع EndEvent: Terminate یا معمولی
            var isTerminateEndEvent = currentNode.ElementType.Contains("terminateEndEvent", StringComparison.OrdinalIgnoreCase);

            if (isTerminateEndEvent)
            {
                // Terminate EndEvent: همه execution contextها را terminate کن
                var allContexts = _contextRepository.GetByInstanceId(context.InstanceId);
                foreach (var ctx in allContexts)
                {
                    if (ctx.State == ExecutionState.Active || ctx.State == ExecutionState.Paused)
                    {
                        ctx.UpdateState(ExecutionState.Terminated);
                        _contextRepository.Save(ctx);
                    }
                }

                // ProcessState را Terminated کن
                processState.Status = ProcessStateStatus.Terminated;
                processState.LastUpdatedAt = DateTime.UtcNow;
                processState.Version++;
                _processStateStore.Save(processState);

                AppendEvent(new ProcessTerminated
                {
                    EventId = Guid.NewGuid(),
                    InstanceId = context.InstanceId,
                    DeploymentId = @event.DeploymentId,
                    DeploymentKey = @event.DeploymentKey,
                    ProcessId = @event.ProcessId,
                    Timestamp = DateTime.UtcNow,
                    TerminationReason = "Terminate EndEvent reached"
                });

                return;
            }

            // EndEvent معمولی: فقط این execution context را complete کن
            context.UpdateState(ExecutionState.Completed);
            _contextRepository.Save(context);

            // بررسی وجود execution context فعال دیگر
            var activeContexts = _contextRepository.GetByInstanceId(context.InstanceId)
                .Where(c => c.State == ExecutionState.Active || c.State == ExecutionState.Paused)
                .ToList();

            // اگر هیچ execution فعالی باقی نمانده، Process را complete کن
            if (!activeContexts.Any())
            {
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
            }

            return;
        }

        if (!topology.Outgoing.TryGetValue(@event.ElementId, out var outgoingTargets) || outgoingTargets.Count == 0)
            return;

        if (!topology.Nodes.TryGetValue(@event.ElementId, out currentNode))
            throw new InvalidOperationException($"Node not found for ElementId '{@event.ElementId}'.");

        var isCurrentGateway = currentNode.IsGateway;

        if (isCurrentGateway)
        {
            // استفاده از GatewayBehavior برای Split
            var gatewayBehavior = _gatewayBehaviorFactory.CreateBehavior(currentNode);
            var sequenceFlows = topology.SequenceFlows.Values
                .Where(f => f.SourceRef == @event.ElementId)
                .ToList();

            var selectedTargets = gatewayBehavior.Split(context, topology, currentNode, outgoingTargets, sequenceFlows);

            if (selectedTargets.Count == 0)
            {
                // هیچ مسیری انتخاب نشد (مثلاً Event-based Gateway)
                return;
            }

            if (selectedTargets.Count == 1)
            {
                // فقط یک مسیر انتخاب شد (Exclusive Gateway یا Gateway با یک outgoing)
                var targetId = selectedTargets[0];
                var sequenceFlow = sequenceFlows.FirstOrDefault(f => f.TargetRef == targetId);

                if (sequenceFlow == null)
                    return;

                context.MoveToNext(targetId);

                if (sequenceFlow.Metadata != null)
                {
                    foreach (var kv in sequenceFlow.Metadata)
                        context.LocalVariables[kv.Key] = kv.Value;
                }

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

                if (!topology.Nodes.TryGetValue(targetId, out var targetNode))
                    throw new InvalidOperationException($"Node not found for target ElementId '{targetId}'.");

                AppendEvent(new ElementCreated
                {
                    EventId = Guid.NewGuid(),
                    DeploymentId = @event.DeploymentId,
                    DeploymentKey = @event.DeploymentKey,
                    InstanceId = @event.InstanceId,
                    ProcessId = @event.ProcessId,
                    ElementId = targetId,
                    ExecutionId = context.ContextId,
                    ElementType = targetNode.ElementType,
                    Timestamp = DateTime.UtcNow,
                    Version = 1,
                    IsExecutable = true
                });
            }
            else
            {
                // چند مسیر انتخاب شد (Parallel یا Inclusive Gateway) - Fork
                context.State = ExecutionState.Completed;
                _contextRepository.Save(context);

                foreach (var targetId in selectedTargets)
                {
                    var sequenceFlow = sequenceFlows.FirstOrDefault(f => f.TargetRef == targetId);
                    if (sequenceFlow == null)
                        continue;

                    var fork = new ExecutionContext()
                    {
                        ContextId = Guid.NewGuid(),
                        InstanceId = context.InstanceId,
                        ParentContextId = context.ContextId,
                        State = ExecutionState.Active,
                        IsExecutable = true,
                        LocalVariables = new Dictionary<string, object?>(context.LocalVariables)
                    };
                    fork.MoveToNext(targetId);
                    fork.LastSequenceFlowId = sequenceFlow.Id;

                    foreach (var kv in sequenceFlow.Metadata)
                        fork.LocalVariables[kv.Key] = kv.Value;

                    _contextRepository.Save(fork);

                    if (!topology.Nodes.TryGetValue(targetId, out var forkTargetNode))
                        throw new InvalidOperationException($"Node not found for target ElementId '{targetId}'.");

                    AppendEvent(new ElementCreated
                    {
                        EventId = Guid.NewGuid(),
                        DeploymentId = @event.DeploymentId,
                        DeploymentKey = @event.DeploymentKey,
                        InstanceId = @event.InstanceId,
                        ProcessId = @event.ProcessId,
                        ElementId = targetId,
                        ExecutionId = fork.ContextId,
                        ElementType = forkTargetNode.ElementType,
                        Timestamp = DateTime.UtcNow,
                        Version = 1,
                        IsExecutable = true
                    });
                }
            }
        }
        else
        {
            // مسیر ساده: implicit XOR - فقط یک مسیر انتخاب می‌شود
            // اگر چند outgoing دارد، باید فقط یکی انتخاب شود (اولین شرط true یا default flow)
            
            string? selectedTargetId = null;
            SequenceFlow? selectedSequenceFlow = null;

            // بررسی شرط‌ها به ترتیب - اولین شرط true انتخاب می‌شود
            foreach (var targetId in outgoingTargets)
            {
                var sequenceFlow = topology.SequenceFlows.Values
                    .FirstOrDefault(f => f.SourceRef == @event.ElementId && f.TargetRef == targetId);

                if (sequenceFlow == null)
                    continue;

                // اگر default flow است و هنوز مسیری انتخاب نشده، برای بعد نگه دار
                if (sequenceFlow.IsDefault && selectedTargetId == null)
                {
                    selectedTargetId = targetId;
                    selectedSequenceFlow = sequenceFlow;
                    continue; // ادامه بده تا ببینیم آیا شرط دیگری true می‌شود
                }

                // بررسی شرط
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
                else if (!sequenceFlow.IsDefault)
                {
                    // اگر شرطی ندارد و default هم نیست، skip کن
                    continue;
                }

                if (conditionOk)
                {
                    selectedTargetId = targetId;
                    selectedSequenceFlow = sequenceFlow;
                    break; // اولین شرط true انتخاب شد
                }
            }

            // اگر هیچ شرطی true نشد و default flow وجود دارد، از آن استفاده کن
            if (selectedTargetId == null && selectedSequenceFlow == null)
            {
                var defaultFlow = topology.SequenceFlows.Values
                    .FirstOrDefault(f => f.SourceRef == @event.ElementId && f.IsDefault);

                if (defaultFlow != null)
                {
                    selectedTargetId = defaultFlow.TargetRef;
                    selectedSequenceFlow = defaultFlow;
                }
                else if (outgoingTargets.Count > 0)
                {
                    // اگر default flow وجود ندارد، اولین flow را انتخاب کن
                    selectedTargetId = outgoingTargets[0];
                    selectedSequenceFlow = topology.SequenceFlows.Values
                        .FirstOrDefault(f => f.SourceRef == @event.ElementId && f.TargetRef == selectedTargetId);
                }
            }

            // اگر مسیری انتخاب شد، ادامه بده
            if (selectedTargetId != null && selectedSequenceFlow != null)
            {
                context.MoveToNext(selectedTargetId);
                context.LastSequenceFlowId = selectedSequenceFlow.Id;

                foreach (var kv in selectedSequenceFlow.Metadata)
                    context.LocalVariables[kv.Key] = kv.Value;

                // اگر نود بعدی یک Gateway باشد، وضعیت را به Completed تغییر بده
                if (topology.Nodes.TryGetValue(selectedTargetId, out var nextNode) && nextNode.IsGateway)
                {
                    context.State = ExecutionState.Completed;
                }
                else
                {
                    context.State = ExecutionState.Active;
                }

                _contextRepository.Save(context);

                if (!topology.Nodes.TryGetValue(selectedTargetId, out var targetNode))
                    throw new InvalidOperationException($"Node not found for target ElementId '{selectedTargetId}'.");

                AppendEvent(new ElementCreated
                {
                    EventId = Guid.NewGuid(),
                    DeploymentId = @event.DeploymentId,
                    DeploymentKey = @event.DeploymentKey,
                    InstanceId = @event.InstanceId,
                    ProcessId = @event.ProcessId,
                    ElementId = selectedTargetId,
                    ExecutionId = context.ContextId,
                    ElementType = targetNode.ElementType,
                    Timestamp = DateTime.UtcNow,
                    Version = 1,
                    IsExecutable = true
                });
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Applies output mappings from node variables to process variables after script execution.
    /// </summary>
    private void ApplyOutputMappings(ElementCompleted evt, ExecutionContext context, ProcessState processState)
    {
        try
        {
            var topology = _topologyStore.Get(evt.DeploymentId, evt.ProcessId);
            if (topology == null || !topology.Nodes.TryGetValue(evt.ElementId, out var node))
            {
                Console.WriteLine($"[IoMapping] Node not found for ElementId: {evt.ElementId}");
                return;
            }

            // Get BonyanIoMapping from node metadata
            if (!node.Metadata.TryGetValue("BonyanIoMapping", out var ioMappingObj) || 
                ioMappingObj is not BonyanIoMapping ioMapping)
            {
                Console.WriteLine($"[IoMapping] No BonyanIoMapping found for ElementId: {evt.ElementId}");
                return;
            }

            Console.WriteLine($"[IoMapping] Applying output mappings for ElementId: {evt.ElementId}");
            Console.WriteLine($"[IoMapping] Node variables before mapping: {string.Join(", ", context.LocalVariables.Select(kv => $"{kv.Key}={kv.Value}"))}");
            Console.WriteLine($"[IoMapping] Process variables before mapping: {string.Join(", ", processState.Variables.Select(kv => $"{kv.Key}={kv.Value}"))}");

            // Apply output mappings: Node Variables → Process Variables
            var result = _ioMappingApplier.ApplyOutputs(
                ioMapping,
                context.LocalVariables,
                processState.Variables
            );

            if (result.Errors.Count > 0)
            {
                Console.WriteLine($"[IoMapping] Errors during output mapping: {string.Join("; ", result.Errors)}");
            }

            Console.WriteLine($"[IoMapping] Applied {result.AppliedMappings.Count} output mappings:");
            foreach (var mapping in result.AppliedMappings)
            {
                Console.WriteLine($"[IoMapping]   {mapping.SourceVariable} → {mapping.TargetVariable} = {mapping.Value}");
            }

            Console.WriteLine($"[IoMapping] Process variables after output mapping: {string.Join(", ", processState.Variables.Select(kv => $"{kv.Key}={kv.Value}"))}");

            // Save process state after output mapping
            if (result.AppliedMappings.Count > 0)
            {
                processState.Version++;
                processState.LastUpdatedAt = DateTime.UtcNow;
                _processStateStore.Save(processState);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IoMapping] Error applying output mappings: {ex.Message}");
            Console.WriteLine($"[IoMapping] Stack trace: {ex.StackTrace}");
        }
    }
}
