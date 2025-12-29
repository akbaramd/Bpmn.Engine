using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class DeploymentActivatedEvent : BaseDomainEvent
{
    public Guid DeploymentId { get; }
    public Guid ProjectId { get; }
    public string DeploymentKey { get; }
    public DateTime ActivatedAt { get; }

    public DeploymentActivatedEvent(Guid deploymentId, Guid projectId, string deploymentKey, DateTime activatedAt)
    {
        DeploymentId = deploymentId;
        ProjectId = projectId;
        DeploymentKey = deploymentKey;
        ActivatedAt = activatedAt;
    }
}

