namespace Novin.Bpmn.Engine.Application.Queries.GetDeployments;

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