using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Core.Process;

public class ProcessCompletedEventHandler : BpmnEventHandlerBase<ProcessCompleted>
{
    private readonly IProcessStateStore _processStateStore;
    private readonly IExecutionContextRepository _contextRepository;
    private readonly ILogger<ProcessCompletedEventHandler> _logger;

    public ProcessCompletedEventHandler(IServiceProvider serviceProvider,
        IProcessStateStore processStateStore,
        ILogger<ProcessCompletedEventHandler> logger, IExecutionContextRepository contextRepository)
        : base(serviceProvider)
    {
        _processStateStore = processStateStore ?? throw new ArgumentNullException(nameof(processStateStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextRepository = contextRepository;
    }

    public override async Task HandleAsync(ProcessCompleted @event, CancellationToken cancellationToken = default)
    {
        var excutions =  _contextRepository.GetByInstanceId(@event.InstanceId);
        var state = _processStateStore.Get(@event.InstanceId);
        if (state != null)
        {
            state.Status = ProcessStateStatus.Completed;
            state.LastUpdatedAt = DateTime.UtcNow;
            _processStateStore.Save(state);

            _logger.LogInformation("Process {InstanceId} marked as Completed", @event.InstanceId);
        }
        else
        {
            _logger.LogWarning("ProcessState not found for Completed event, InstanceId: {InstanceId}", @event.InstanceId);
        }

        await Task.CompletedTask;
    }
}