using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class DeploymentCreatedEvent : BaseDomainEvent
{
    public Guid DeploymentId { get; }
    public string DeploymentKey { get; }
    public int Version { get; } public Guid ProjectId { get; }
    public DateTime DeployedAt { get; }

    public DeploymentCreatedEvent(Guid deploymentId, Guid projectId , string deploymentKey, int version, DateTime deployedAt)
    {
        DeploymentId = deploymentId;
        ProjectId = projectId;
        DeploymentKey = deploymentKey;
        Version = version;
        DeployedAt = deployedAt;
    }
}

