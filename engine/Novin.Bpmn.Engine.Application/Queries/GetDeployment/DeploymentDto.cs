namespace Novin.Bpmn.Engine.Application.Queries.GetDeployment;

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