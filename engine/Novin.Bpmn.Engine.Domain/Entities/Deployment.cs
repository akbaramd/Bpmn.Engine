using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Domain.Entities;

/// <summary>
/// Aggregate root representing a BPMN process definition deployment.
/// Each deployment belongs to a Project.
/// </summary>
public sealed class Deployment : BaseAggregateRoot
{
    private const string BpmnModelNs = "http://www.omg.org/spec/BPMN/20100524/MODEL";

    // XmlSerializer ساختنش گرونه => static
    private static readonly XmlSerializer DefinitionsSerializer =
        new(typeof(BpmnDefinitions), BpmnModelNs);

    // cache (EF persist نمی‌کند)
    private BpmnDefinitions? _definitionsCache;
    private string? _definitionsCacheForHash;

    public Guid ProjectId { get; private set; }

    public string DeploymentKey { get; private set; } = default!;
    public string BpmnXml { get; private set; } = default!;
    public string? Label { get; private set; }

    public DateTime DeployedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public int Version { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>
    /// Optional but very useful: lets you cache parsed definitions safely and detect changes.
    /// </summary>
    public string BpmnHash { get; private set; } = default!;

    private Deployment()
    {
        DeployedAtUtc = DateTime.UtcNow;
        Version = 1;
        IsActive = true;
        UpdatedAtUtc = DeployedAtUtc;
        
    }

    public static Deployment Create(
        Guid projectId,
        string deploymentKey,
        string bpmnXml,
        string? label = null)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId cannot be empty.", nameof(projectId));
        if (string.IsNullOrWhiteSpace(deploymentKey)) throw new ArgumentException("DeploymentKey cannot be empty.", nameof(deploymentKey));
        if (string.IsNullOrWhiteSpace(bpmnXml)) throw new ArgumentException("BpmnXml cannot be empty.", nameof(bpmnXml));

        var d = new Deployment
        {
            ProjectId = projectId,
            DeploymentKey = deploymentKey.Trim(),
            BpmnXml = bpmnXml,
            Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
            DeployedAtUtc = DateTime.UtcNow,
            Version = 1,
            IsActive = true,
        };

        d.BpmnHash = ComputeSha256(d.BpmnXml);
        d.InvalidateDefinitionsCache();

        d.AddDomainEvent(new DeploymentCreatedEvent(
            d.Id, d.ProjectId, d.DeploymentKey, d.Version, d.DeployedAtUtc));

        return d;
    }

    /// <summary>
    /// Creates next version based on current deployment (immutable versioning style).
    /// Recommended approach: instead of UpdateVersion + UpdateBpmnXml on same row.
    /// </summary>
    public Deployment CreateNextVersion(string newBpmnXml, string? newLabel = null)
    {
        if (string.IsNullOrWhiteSpace(newBpmnXml))
            throw new ArgumentException("BpmnXml cannot be empty.", nameof(newBpmnXml));

        var next = new Deployment
        {
            ProjectId = ProjectId,
            DeploymentKey = DeploymentKey,
            BpmnXml = newBpmnXml,
            Label = string.IsNullOrWhiteSpace(newLabel) ? Label : newLabel.Trim(),
            DeployedAtUtc = DateTime.UtcNow,
            Version = checked(Version + 1),
            IsActive = true
        };

        next.BpmnHash = ComputeSha256(next.BpmnXml);
        next.InvalidateDefinitionsCache();

        next.AddDomainEvent(new DeploymentCreatedEvent(
            next.Id, next.ProjectId, next.DeploymentKey, next.Version, next.DeployedAtUtc));

        // Optional: deactivate this one automatically
        // this.Deactivate("New version created.");

        return next;
    }

    public void Deactivate(string? reason = null)
    {
        if (!IsActive) return;

        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new DeploymentDeactivatedEvent(
            Id, ProjectId, DeploymentKey,  reason,DateTime.UtcNow));
    }

    public void Activate()
    {
        if (IsActive) return;

        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new DeploymentActivatedEvent(
            Id, ProjectId, DeploymentKey, DateTime.UtcNow));
    }

    public void UpdateLabel(string? label)
    {
        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new DeploymentUpdatedEvent(
            Id, ProjectId, DeploymentKey, "Label", DateTime.UtcNow));
    }

    /// <summary>
    /// If you keep same row and mutate XML, do it here.
    /// (But recommended: CreateNextVersion instead.)
    /// </summary>
    public void UpdateBpmnXml(string bpmnXml)
    {
        if (string.IsNullOrWhiteSpace(bpmnXml))
            throw new ArgumentException("BpmnXml cannot be null or empty", nameof(bpmnXml));

        BpmnXml = bpmnXml;
        UpdatedAtUtc = DateTime.UtcNow;

        BpmnHash = ComputeSha256(BpmnXml);
        InvalidateDefinitionsCache();

        AddDomainEvent(new DeploymentUpdatedEvent(
            Id, ProjectId, DeploymentKey, "BpmnXml", DateTime.UtcNow));
    }

    /// <summary>
    /// Parse BPMN XML and return BpmnDefinitions.
    /// Cached by BpmnHash to avoid expensive re-parsing.
    /// </summary>
    public BpmnDefinitions GetDefinitions()
    {
        if (string.IsNullOrWhiteSpace(BpmnXml))
            throw new InvalidOperationException("BPMN XML is empty or null.");

        // اگر cache برای همین hash هست => reuse
        if (_definitionsCache is not null && _definitionsCacheForHash == BpmnHash)
            return _definitionsCache;

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, // security
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreWhitespace = true,
                CloseInput = true
            };

            using var sr = new StringReader(BpmnXml);
            using var xr = XmlReader.Create(sr, settings);

            var obj = DefinitionsSerializer.Deserialize(xr);
            if (obj is not BpmnDefinitions defs)
                throw new InvalidOperationException("Failed to deserialize BPMN XML.");

            _definitionsCache = defs;
            _definitionsCacheForHash = BpmnHash;
            return defs;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse BPMN XML: {ex.Message}", ex);
        }
    }

    private void InvalidateDefinitionsCache()
    {
        _definitionsCache = null;
        _definitionsCacheForHash = null;
    }

    private static string ComputeSha256(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash); // .NET 5+
    }
}
