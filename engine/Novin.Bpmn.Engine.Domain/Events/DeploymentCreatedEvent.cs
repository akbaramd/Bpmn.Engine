using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class DeploymentCreatedEvent : BaseDomainEvent
{
    public Guid DeploymentId { get; }
    public string DeploymentKey { get; }
    public int Version { get; }
    public DateTime DeployedAt { get; }

    public DeploymentCreatedEvent(Guid deploymentId, string deploymentKey, int version, DateTime deployedAt)
    {
        DeploymentId = deploymentId;
        DeploymentKey = deploymentKey;
        Version = version;
        DeployedAt = deployedAt;
    }
}

