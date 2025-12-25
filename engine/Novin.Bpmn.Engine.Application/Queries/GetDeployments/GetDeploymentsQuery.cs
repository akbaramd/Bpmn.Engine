using MediatR;

namespace Novin.Bpmn.Engine.Application.Queries.GetDeployments;

/// <summary>
/// Query to get all deployments with optional filtering
/// </summary>
public record GetDeploymentsQuery(
    string? DeploymentKey = null,
    bool ActiveOnly = false
) : IRequest<IEnumerable<DeploymentDto>>;

/// <summary>
/// DTO for deployment information (shared with GetDeployment)
/// </summary>
public record DeploymentDto(
    Guid Id,
    string DeploymentKey,
    string Label,
    int Version,
    string BpmnXml,
    DateTime DeployedAt,
    bool IsActive
);