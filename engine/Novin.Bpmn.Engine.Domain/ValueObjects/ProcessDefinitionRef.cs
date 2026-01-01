namespace Novin.Bpmn.Engine.Domain.ValueObjects;

/// <summary>
/// Unique reference to a BPMN process definition.
/// Key: (DeploymentId, ProcessBpmnId, Version)
/// </summary>
public sealed record ProcessDefinitionRef
{
    public Guid DeploymentId { get; }
    public string ProcessBpmnId { get; }
    public int Version { get; }

    public ProcessDefinitionRef(Guid deploymentId, string processBpmnId, int version)
    {
        if (deploymentId == Guid.Empty)
            throw new ArgumentException("DeploymentId cannot be empty", nameof(deploymentId));
        if (string.IsNullOrWhiteSpace(processBpmnId))
            throw new ArgumentException("ProcessBpmnId cannot be empty", nameof(processBpmnId));
        if (version < 1)
            throw new ArgumentException("Version must be >= 1", nameof(version));

        DeploymentId = deploymentId;
        ProcessBpmnId = processBpmnId.Trim();
        Version = version;
    }

    public string ToCacheKey() => $"{DeploymentId}:{ProcessBpmnId}:{Version}";

    /// <summary>
    /// Create ProcessDefinitionRef from Process and Deployment.
    /// </summary>
    public static ProcessDefinitionRef From(Entities.Process process, Entities.Deployment deployment)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (deployment is null) throw new ArgumentNullException(nameof(deployment));
        if (process.DeploymentId != deployment.Id)
            throw new ArgumentException("Process.DeploymentId must match Deployment.Id");

        return new ProcessDefinitionRef(deployment.Id, process.ProcessBpmnId, deployment.Version);
    }
}

