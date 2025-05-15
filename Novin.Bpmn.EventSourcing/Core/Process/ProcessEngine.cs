using Novin.Bpmn.EventSourcing.Core.EventStore;
using Novin.Bpmn.EventSourcing.Core.Process;
using Novin.Bpmn.EventSourcing.Events;

public class ProcessEngine : IProcessEngine
{
    private readonly IBpmnDeploymentStore _deploymentStore;
    private readonly IFlowTopologyStore _topologyStore;
    private readonly IEventStore _eventStore;

    public ProcessEngine(
        IBpmnDeploymentStore deploymentStore,
        IFlowTopologyStore topologyStore,
        IEventStore eventStore)
    {
        _deploymentStore = deploymentStore;
        _topologyStore = topologyStore;
        _eventStore = eventStore;
    }

    public async Task StartProcessAsync(string deploymentKey, string processId, Guid instanceId, CancellationToken cancellationToken = default)
    {
        // 1. دریافت آخرین deployment
        var deployment = _deploymentStore.GetLatest(deploymentKey);
        if (deployment == null)
            throw new InvalidOperationException($"Deployment with key {deploymentKey} not found");

        // 2. دریافت توپولوژی مرتبط
        var topology = _topologyStore.Get(deployment.DeploymentId, processId);
        if (topology == null)
            throw new InvalidOperationException($"Topology for process {processId} not found");

        // 3. ثبت رویداد ProcessCreatedEvent
        var processCreatedEvent = new ProcessStarted()
        {
            EventId = Guid.NewGuid(),
            InstanceId = instanceId,
            DeploymentKey = deploymentKey,
            DeploymentId = deployment.DeploymentId,
            ProcessId = processId,
            Timestamp = DateTime.UtcNow
        };

        _eventStore.Append(processCreatedEvent);

        // رویداد منتشر شد، هندلر مربوطه باید Start Task را بیابد و ElementCreatedEvent بسازد

        await Task.CompletedTask;
    }
}
