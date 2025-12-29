using System.Xml.Serialization;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Domain.Entities;

/// <summary>
/// Aggregate root representing a BPMN process definition deployment
/// </summary>
public class Deployment : BaseAggregateRoot
{
    public string DeploymentKey { get; private set; }
    public string BpmnXml { get; private set; }
    public string? Label { get; private set; }
    public DateTime DeployedAt { get; private set; }
    public int Version { get; private set; }
    public bool IsActive { get; private set; }

    private Deployment() : base()
    {
        DeployedAt = DateTime.UtcNow;
        Version = 1;
        IsActive = true;
    }

    public Deployment(string deploymentKey, string bpmnXml, string? label = null) : this()
    {
        if (string.IsNullOrWhiteSpace(deploymentKey))
            throw new ArgumentException("Deployment key cannot be null or empty", nameof(deploymentKey));
        
        if (string.IsNullOrWhiteSpace(bpmnXml))
            throw new ArgumentException("BPMN XML cannot be null or empty", nameof(bpmnXml));

        DeploymentKey = deploymentKey;
        BpmnXml = bpmnXml;
        Label = label;

        AddDomainEvent(new DeploymentCreatedEvent(Id, DeploymentKey, Version, DeployedAt));
    }

    public Deployment(string deploymentKey, string bpmnXml, int version, string? label = null) : this(deploymentKey, bpmnXml, label)
    {
        if (version < 1)
            throw new ArgumentException("Version must be greater than or equal to 1", nameof(version));

        Version = version;
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidOperationException("Deployment is already inactive.");

        IsActive = false;
        
        AddDomainEvent(new DeploymentDeactivatedEvent(Id, DeploymentKey, DateTime.UtcNow));
    }

    public void Activate()
    {
        if (IsActive)
            throw new InvalidOperationException("Deployment is already active.");

        IsActive = true;
        
        AddDomainEvent(new DeploymentActivatedEvent(Id, DeploymentKey, DateTime.UtcNow));
    }

    public void UpdateVersion(int newVersion)
    {
        if (newVersion <= Version)
            throw new ArgumentException("New version must be greater than current version", nameof(newVersion));

        Version = newVersion;
        
        AddDomainEvent(new DeploymentVersionUpdatedEvent(Id, DeploymentKey, Version, DateTime.UtcNow));
    }

    /// <summary>
    /// Update the BPMN XML content of this deployment
    /// </summary>
    public void UpdateBpmnXml(string bpmnXml)
    {
        if (string.IsNullOrWhiteSpace(bpmnXml))
            throw new ArgumentException("BPMN XML cannot be null or empty", nameof(bpmnXml));

        BpmnXml = bpmnXml;

        AddDomainEvent(new DeploymentUpdatedEvent(Id, DeploymentKey, "BpmnXml", DateTime.UtcNow));
    }

    /// <summary>
    /// Update the label of this deployment
    /// </summary>
    public void UpdateLabel(string? label)
    {
        Label = label;

        AddDomainEvent(new DeploymentUpdatedEvent(Id, DeploymentKey, "Label", DateTime.UtcNow));
    }

    /// <summary>
    /// Parses the BPMN XML and returns BpmnDefinitions model
    /// </summary>
    public BpmnDefinitions GetDefinitions()
    {
        if (string.IsNullOrWhiteSpace(BpmnXml))
            throw new InvalidOperationException("BPMN XML is empty or null.");

        try
        {
            var serializer = new XmlSerializer(typeof(BpmnDefinitions),
                "http://www.omg.org/spec/BPMN/20100524/MODEL");
            
            using var stringReader = new StringReader(BpmnXml);
            var definitions = serializer.Deserialize(stringReader) as BpmnDefinitions;
            
            if (definitions == null)
                throw new InvalidOperationException("Failed to deserialize BPMN XML.");
            
            return definitions;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse BPMN XML: {ex.Message}", ex);
        }
    }
}

