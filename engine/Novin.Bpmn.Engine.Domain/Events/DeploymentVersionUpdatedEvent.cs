using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class DeploymentVersionUpdatedEvent : BaseDomainEvent
{
    public Guid DeploymentId { get; }
    public string DeploymentKey { get; }
    public int Version { get; }
    public DateTime UpdatedAt { get; }

    public DeploymentVersionUpdatedEvent(Guid deploymentId, string deploymentKey, int version, DateTime updatedAt)
    {
        DeploymentId = deploymentId;
        DeploymentKey = deploymentKey;
        Version = version;
        UpdatedAt = updatedAt;
    }
}

