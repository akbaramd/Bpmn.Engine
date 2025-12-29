using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

/// <summary>
/// Domain event raised when a deployment is updated
/// </summary>
public sealed record DeploymentUpdatedEvent(
    Guid DeploymentId,
    Guid ProjectId,
    string DeploymentKey,
    string UpdatedField,
    DateTime UpdatedAt
) : IDomainEvent;