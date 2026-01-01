namespace Novin.Bpmn.Engine.Application.Features.Deployments.Commands.UpdateDeployment;

public sealed record UpdateDeploymentResult(
    Guid DeploymentId,
    Guid ProjectId,
    string DeploymentKey,
    string? Label,
    int Version,
    DateTime DeployedAtUtc,
    bool IsActive,
    bool IsNewVersion // true if a new version was created
);

