using Novin.Bpmn.EventSourcing.Core.Process;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// Handler برای VariableSet event - به‌روزرسانی ProcessState با optimistic concurrency
/// </summary>
public class VariableSetEventHandler : BpmnEventHandlerBase<VariableSet>
{
    private readonly IProcessStateStore _processStateStore;

    public VariableSetEventHandler(IServiceProvider serviceProvider, IProcessStateStore processStateStore)
        : base(serviceProvider)
    {
        _processStateStore = processStateStore ?? throw new ArgumentNullException(nameof(processStateStore));
    }

    public override async Task HandleAsync(VariableSet @event, CancellationToken cancellationToken = default)
    {
        if (@event.Scope != VariableScope.Process)
        {
            // برای ExecutionContext scope، نیازی به به‌روزرسانی ProcessState نیست
            await Task.CompletedTask;
            return;
        }

        var processState = _processStateStore.Get(@event.InstanceId);
        if (processState == null)
        {
            throw new InvalidOperationException($"ProcessState not found for InstanceId {@event.InstanceId}");
        }

        // به‌روزرسانی متغیر با optimistic concurrency
        var originalVersion = processState.Version;
        processState.Variables[@event.VariableName] = @event.VariableValue;
        processState.Version++;
        processState.LastUpdatedAt = DateTime.UtcNow;

        // TODO: اگر ProcessStateStore از optimistic concurrency پشتیبانی می‌کند، باید version check انجام شود
        _processStateStore.Save(processState);

        await Task.CompletedTask;
    }
}

/// <summary>
/// Handler برای VariablesSet event - به‌روزرسانی چند متغیر به صورت batch
/// </summary>
public class VariablesSetEventHandler : BpmnEventHandlerBase<VariablesSet>
{
    private readonly IProcessStateStore _processStateStore;

    public VariablesSetEventHandler(IServiceProvider serviceProvider, IProcessStateStore processStateStore)
        : base(serviceProvider)
    {
        _processStateStore = processStateStore ?? throw new ArgumentNullException(nameof(processStateStore));
    }

    public override async Task HandleAsync(VariablesSet @event, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[VariablesSetEventHandler] Received VariablesSet event (EventId: {@event.EventId}, Scope: {@event.Scope}, InstanceId: {@event.InstanceId})");
        Console.WriteLine($"[VariablesSetEventHandler] Variables in event: {string.Join(", ", @event.Variables.Select(kv => $"{kv.Key}={kv.Value}"))}");
        
        if (@event.Scope != VariableScope.Process)
        {
            Console.WriteLine($"[VariablesSetEventHandler] Skipping - Scope is {(@event.Scope)}, not Process");
            await Task.CompletedTask;
            return;
        }

        var processState = _processStateStore.Get(@event.InstanceId);
        if (processState == null)
        {
            Console.WriteLine($"[VariablesSetEventHandler] ERROR: ProcessState not found for InstanceId {@event.InstanceId}");
            throw new InvalidOperationException($"ProcessState not found for InstanceId {@event.InstanceId}");
        }

        Console.WriteLine($"[VariablesSetEventHandler] Current ProcessState variables before update: {string.Join(", ", processState.Variables.Select(kv => $"{kv.Key}={kv.Value}"))}");

        // به‌روزرسانی متغیرها
        foreach (var kv in @event.Variables)
        {
            var oldValue = processState.Variables.TryGetValue(kv.Key, out var old) ? old : null;
            processState.Variables[kv.Key] = kv.Value;
            Console.WriteLine($"[VariablesSetEventHandler] Updated variable '{kv.Key}': {oldValue} -> {kv.Value}");
        }

        processState.Version++;
        processState.LastUpdatedAt = DateTime.UtcNow;
        _processStateStore.Save(processState);
        
        Console.WriteLine($"[VariablesSetEventHandler] ProcessState saved. Final variables: {string.Join(", ", processState.Variables.Select(kv => $"{kv.Key}={kv.Value}"))}");
        Console.WriteLine($"[VariablesSetEventHandler] ProcessState Version: {processState.Version}");

        await Task.CompletedTask;
    }
}

/// <summary>
/// Handler برای VariablesMerged event - log کردن merge
/// </summary>
public class VariablesMergedEventHandler : BpmnEventHandlerBase<VariablesMerged>
{
    private readonly IProcessStateStore _processStateStore;

    public VariablesMergedEventHandler(IServiceProvider serviceProvider, IProcessStateStore processStateStore)
        : base(serviceProvider)
    {
        _processStateStore = processStateStore ?? throw new ArgumentNullException(nameof(processStateStore));
    }

    public override async Task HandleAsync(VariablesMerged @event, CancellationToken cancellationToken = default)
    {
        var processState = _processStateStore.Get(@event.InstanceId);
        if (processState == null)
        {
            throw new InvalidOperationException($"ProcessState not found for InstanceId {@event.InstanceId}");
        }

        // به‌روزرسانی ProcessState با متغیرهای merge شده
        foreach (var kv in @event.MergedVariables)
        {
            processState.Variables[kv.Key] = kv.Value;
        }

        processState.Version++;
        processState.LastUpdatedAt = DateTime.UtcNow;
        _processStateStore.Save(processState);

        await Task.CompletedTask;
    }
}

