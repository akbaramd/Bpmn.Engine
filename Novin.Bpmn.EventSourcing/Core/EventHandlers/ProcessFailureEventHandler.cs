using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Core.Process;

public class ProcessFailureEventHandler : BpmnEventHandlerBase<ProcessFailureEvent>
{
    private readonly IProcessStateStore _processStateStore;
    private readonly ILogger<ProcessFailureEventHandler> _logger;

    public ProcessFailureEventHandler(IServiceProvider serviceProvider,
        IProcessStateStore processStateStore,
        ILogger<ProcessFailureEventHandler> logger)
        : base(serviceProvider)
    {
        _processStateStore = processStateStore ?? throw new ArgumentNullException(nameof(processStateStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task HandleAsync(ProcessFailureEvent @event, CancellationToken cancellationToken = default)
    {
        var state = _processStateStore.Get(@event.InstanceId);
        if (state != null)
        {
            state.Status = ProcessStateStatus.Failed;
            state.LastUpdatedAt = DateTime.UtcNow;
            _processStateStore.Save(state);

            _logger.LogError("Process failure detected. InstanceId: {InstanceId}, Reason: {Reason}", @event.InstanceId, @event.FailureReason);
        }
        else
        {
            _logger.LogWarning("ProcessState not found for Failure event, InstanceId: {InstanceId}", @event.InstanceId);
        }

        await Task.CompletedTask;
    }
}