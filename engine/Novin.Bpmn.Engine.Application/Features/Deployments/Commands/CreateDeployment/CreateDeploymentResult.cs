namespace Novin.Bpmn.Engine.Application.Features.Deployments.Commands.CreateDeployment;

public sealed record CreateDeploymentResult(
    Guid DeploymentId,
    Guid ProjectId,
    string DeploymentKey,
    string? Label,
    int Version,
    DateTime DeployedAtUtc,
    bool IsActive
);

