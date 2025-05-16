namespace Novin.Bpmn.EventSourcing.Core.Process;

public interface IProcessEngine
{
    Task StartProcessAsync(string deploymentKey, string processId, Guid instanceId,Dictionary<string,object?>? initializeVariables = null, CancellationToken cancellationToken = default);
}