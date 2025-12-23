using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class DeploymentDeactivatedEvent : BaseDomainEvent
{
    public Guid DeploymentId { get; }
    public string DeploymentKey { get; }
    public DateTime DeactivatedAt { get; }

    public DeploymentDeactivatedEvent(Guid deploymentId, string deploymentKey, DateTime deactivatedAt)
    {
        DeploymentId = deploymentId;
        DeploymentKey = deploymentKey;
        DeactivatedAt = deactivatedAt;
    }
}

