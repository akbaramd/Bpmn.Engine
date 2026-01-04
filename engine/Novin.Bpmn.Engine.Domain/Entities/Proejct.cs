using System;
using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Entities;

/// <summary>
/// Project offering a logical grouping for definitions (deployments).
/// Each Deployment belongs to exactly one Project.
/// </summary>
public sealed class Project : BaseAggregateRoot
{
    public string Key { get; private set; } = default!;     // unique (e.g. "hr-onboarding")
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private Project()
    {
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Project Create(Guid id,string key, string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Project key cannot be empty.", nameof(key));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name cannot be empty.", nameof(name));

        var p = new Project
        {
            Id = id,
            Key = key.Trim(),
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
        };

        p.AddDomainEvent(new ProjectCreatedEvent(
            ProjectId: p.Id,
            Key: p.Key,
            Name: p.Name,
            OccurredAtUtc: p.CreatedAtUtc));

        return p;
    }

    public void Rename(string name)
    {
        EnsureActive();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Name = name.Trim();
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new ProjectRenamedEvent(Id, Name, UpdatedAtUtc.Value));
    }

    public void UpdateDescription(string? description)
    {
        EnsureActive();

        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new ProjectDescriptionUpdatedEvent(Id, Description, UpdatedAtUtc.Value));
    }

    public void Deactivate(string? reason = null)
    {
        if (!IsActive) return;

        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new ProjectDeactivatedEvent(Id, reason, UpdatedAtUtc.Value));
    }

    public void Activate()
    {
        if (IsActive) return;

        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new ProjectActivatedEvent(Id, UpdatedAtUtc.Value));
    }

    private void EnsureActive()
    {
        if (!IsActive)
            throw new InvalidOperationException("Project is inactive.");
    }
}


public sealed record ProjectCreatedEvent(
    Guid ProjectId,
    string Key,
    string Name,
    DateTime OccurredAtUtc
) : IDomainEvent;

public sealed record ProjectRenamedEvent(
    Guid ProjectId,
    string Name,
    DateTime OccurredAtUtc
) : IDomainEvent;

public sealed record ProjectDescriptionUpdatedEvent(
    Guid ProjectId,
    string? Description,
    DateTime OccurredAtUtc
) : IDomainEvent;

public sealed record ProjectActivatedEvent(
    Guid ProjectId,
    DateTime OccurredAtUtc
) : IDomainEvent;

public sealed record ProjectDeactivatedEvent(
    Guid ProjectId,
    string? Reason,
    DateTime OccurredAtUtc
) : IDomainEvent;
