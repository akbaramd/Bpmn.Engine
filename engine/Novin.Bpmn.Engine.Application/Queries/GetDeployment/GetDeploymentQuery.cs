using MediatR;

namespace Novin.Bpmn.Engine.Application.Queries.GetDeployment;

/// <summary>
/// Query to get a specific deployment by ID
/// </summary>
public record GetDeploymentQuery(Guid DeploymentId) : IRequest<DeploymentDto?>;