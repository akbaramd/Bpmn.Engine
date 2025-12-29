using MediatR;

namespace Novin.Bpmn.Engine.Application.Queries.GetDeployments;

/// <summary>
/// Query to get all deployments with optional filtering
/// </summary>
public record GetDeploymentsQuery(
    string? DeploymentKey = null,
    bool ActiveOnly = false
) : IRequest<IEnumerable<DeploymentDto>>;