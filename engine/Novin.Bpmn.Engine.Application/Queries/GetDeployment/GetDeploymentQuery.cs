using MediatR;

namespace Novin.Bpmn.Engine.Application.Queries.GetDeployment;

/// <summary>
/// Query to get a specific deployment by ID
/// </summary>
public record GetDeploymentQuery(Guid DeploymentId) : IRequest<DeploymentDto?>;

/// <summary>
/// DTO for deployment information
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