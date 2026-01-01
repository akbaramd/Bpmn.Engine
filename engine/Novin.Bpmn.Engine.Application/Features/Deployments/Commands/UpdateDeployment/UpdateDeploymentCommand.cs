using MediatR;

namespace Novin.Bpmn.Engine.Application.Features.Deployments.Commands.UpdateDeployment;

public sealed record UpdateDeploymentCommand(
    Guid DeploymentId,
    string? BpmnXml = null,
    string? Label = null,
    int? RequestedVersion = null // If provided and > current version, create new version
) : IRequest<UpdateDeploymentResult>;

