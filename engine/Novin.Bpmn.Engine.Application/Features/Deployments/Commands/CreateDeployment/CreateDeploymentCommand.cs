using MediatR;

namespace Novin.Bpmn.Engine.Application.Features.Deployments.Commands.CreateDeployment;

public sealed record CreateDeploymentCommand(
    Guid ProjectId,
    string DeploymentKey,
    string BpmnXml,
    string? Label = null
) : IRequest<CreateDeploymentResult>;

