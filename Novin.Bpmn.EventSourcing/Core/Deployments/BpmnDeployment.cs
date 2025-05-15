namespace Novin.Bpmn.EventSourcing.Core.Deployments;

public class BpmnDeployment
{
    public Guid DeploymentId { get; init; } = Guid.NewGuid();
    public string DeploymentKey { get; init; } = default!;
    public string Version { get; init; } = "1.0.0";
    public string XmlContent { get; init; } = default!;
    public DateTime DeployedAt { get; init; } = DateTime.UtcNow;

    // Optional fields
    public string? Name { get; init; }
    public string? TenantId { get; init; }
    public Dictionary<string, object?> Metadata { get; init; } = new();
}