namespace Novin.Bpmn.EventSourcing.Core.Models;

/// <summary>
/// Persisted representation of a deployed BPMN process definition.
/// </summary>
public class ProcessDeploymentState
{
    /// <summary>
    /// Primary key for storage.
    /// </summary>
    public Guid DeploymentId { get; set; }

    /// <summary>
    /// Unique key for the deployment (e.g. engine‐generated).
    /// </summary>
    public string DeploymentKey { get; set; } = string.Empty;

    /// <summary>
    /// Version of the deployment/definition.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Optional human‐friendly label.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// The raw BPMN XML deployed.
    /// </summary>
    public string XmlContent { get; set; } = string.Empty;

    /// <summary>
    /// When the process definition was deployed.
    /// </summary>
    public DateTime DeploymentTime { get; set; }
}