using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class DeploymentActivatedEvent : BaseDomainEvent
{
    public Guid DeploymentId { get; }
    public string DeploymentKey { get; }
    public DateTime ActivatedAt { get; }

    public DeploymentActivatedEvent(Guid deploymentId, string deploymentKey, DateTime activatedAt)
    {
        DeploymentId = deploymentId;
        DeploymentKey = deploymentKey;
        ActivatedAt = activatedAt;
    }
}

