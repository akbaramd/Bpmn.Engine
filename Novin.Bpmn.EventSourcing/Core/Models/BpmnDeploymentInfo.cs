using System;

namespace Novin.Bpmn.EventSourcing.Core.Models;

/// <summary>
/// Information about a deployed BPMN process definition
/// </summary>
public class BpmnDeploymentInfo
{
    /// <summary>
    /// Unique key for the deployment
    /// </summary>
    public string DeploymentKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Unique identifier for the definition
    /// </summary>
    public string DefinitionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Version of the deployment
    /// </summary>
    public int Version { get; set; }
    
    /// <summary>
    /// Optional label for the deployment
    /// </summary>
    public string? Label { get; set; }
    
    /// <summary>
    /// BPMN XML content
    /// </summary>
    public string XmlContent { get; set; } = string.Empty;
    
    /// <summary>
    /// Time when the definition was deployed
    /// </summary>
    public DateTime DeploymentTime { get; set; }
} 