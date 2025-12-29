using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public class DeploymentDeactivatedEvent : BaseDomainEvent
{
    public Guid DeploymentId { get; }
    public Guid ProjectId { get; }
    public string DeploymentKey { get; }
    public string? Reason { get; }
    public DateTime DeactivatedAt { get; }

    public DeploymentDeactivatedEvent(Guid deploymentId, Guid projectId, string deploymentKey, string? reason, DateTime deactivatedAt)
    {
        DeploymentId = deploymentId;
        ProjectId = projectId;
        DeploymentKey = deploymentKey;
        Reason = reason;
        DeactivatedAt = deactivatedAt;
    }
}

