namespace Novin.Bpmn.EventSourcing.Core.Process;

public interface IProcessEngine
{
    Task StartProcessAsync(string deploymentKey, string processId, Guid instanceId, CancellationToken cancellationToken = default);
}