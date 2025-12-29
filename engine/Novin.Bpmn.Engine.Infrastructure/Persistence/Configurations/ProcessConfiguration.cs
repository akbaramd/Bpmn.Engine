// Infrastructure/Persistence/Configurations/ProcessConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Infrastructure.Common;
using Novin.Bpmn.Engine.Infrastructure.Persistence.Ef;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Configurations;

public sealed class ProcessConfiguration : IEntityTypeConfiguration<Process>
{
    public void Configure(EntityTypeBuilder<Process> entity)
    {
        entity.ToTable("Processes");

        entity.HasKey(x => x.Id);

        // ---------- Required scalars ----------
        entity.Property(x => x.ProjectId).IsRequired();
        entity.Property(x => x.DeploymentId).IsRequired();

        entity.Property(x => x.ProcessBpmnId)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(x => x.BusinessKey)
            .HasMaxLength(500);

        entity.Property(x => x.State)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        entity.Property(x => x.CreatedAtUtc).IsRequired();

        entity.Property(x => x.StartedAtUtc);
        entity.Property(x => x.CompletedAtUtc);
        entity.Property(x => x.FailedAtUtc);
        entity.Property(x => x.TerminatedAtUtc);

        entity.Property(x => x.FailureReason)
            .HasMaxLength(2000);

        entity.Property(x => x.TerminationReason)
            .HasMaxLength(2000);

        // ---------- Domain collections (IDs only) ----------
        // Exposed as IReadOnlyCollection<Guid>, backed by private HashSet<Guid> fields:
        //   _tokenIds, _nodeInstanceIds
        // Persist them as JSON columns (single-row, snapshot-friendly).
        // If you prefer normalized tables later, you can replace these with owned tables.

        entity.Ignore(x => x.TokenIds);
        entity.Ignore(x => x.NodeInstanceIds);

        // _tokenIds -> JSON
     
        var tokenIdsComparer = new ValueComparer<HashSet<Guid>>(
            (a, b) => EfComparers.GuidSetEqual(a, b),
            v => EfComparers.GuidSetHash(v),
            v => EfComparers.GuidSetSnapshot(v));

        entity.Property<HashSet<Guid>>("_tokenIds")
            .HasColumnName("TokenIds")
            .HasColumnType("text")
            .HasConversion(
                v => JsonHelper.SerializeObject(v ?? new HashSet<Guid>()),
                v => JsonHelper.DeserializeObject<HashSet<Guid>>(v) ?? new HashSet<Guid>())
            .Metadata.SetValueComparer(tokenIdsComparer);

        var nodeIdsComparer = new ValueComparer<HashSet<Guid>>(
            (a, b) => EfComparers.GuidSetEqual(a, b),
            v => EfComparers.GuidSetHash(v),
            v => EfComparers.GuidSetSnapshot(v));

        entity.Property<HashSet<Guid>>("_nodeInstanceIds")
            .HasColumnName("NodeInstanceIds")
            .HasColumnType("text")
            .HasConversion(
                v => JsonHelper.SerializeObject(v ?? new HashSet<Guid>()),
                v => JsonHelper.DeserializeObject<HashSet<Guid>>(v) ?? new HashSet<Guid>())
            .Metadata.SetValueComparer(nodeIdsComparer);



        // ---------- Variables (Dictionary<string,string>) ----------
        entity.Ignore(x => x.Variables);

        var varsComparer = new ValueComparer<Dictionary<string, string>>(
            (a, b) => EfComparers.VarsEqual(a, b),
            v => EfComparers.VarsHash(v),
            v => EfComparers.VarsSnapshot(v));

        entity.Property<Dictionary<string, string>>("_variables")
            .HasColumnName("Variables")
            .HasColumnType("text")
            .HasConversion(
                v => JsonHelper.SerializeObject(v ?? new Dictionary<string, string>(StringComparer.Ordinal)),
                v => JsonHelper.DeserializeObject<Dictionary<string, string>>(v) ?? new Dictionary<string, string>(StringComparer.Ordinal))
            .Metadata.SetValueComparer(varsComparer);

        // ---------- Indexes ----------
        // Multi-tenant boundary
        entity.HasIndex(x => x.ProjectId);

        // Most common lookups
        entity.HasIndex(x => x.DeploymentId);
        entity.HasIndex(x => new { x.ProjectId, x.State });
        entity.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });

        // BusinessKey queries (optional uniqueness is enforced by app/domain rules)
        entity.HasIndex(x => new { x.ProjectId, x.BusinessKey })
            .HasFilter("[BusinessKey] IS NOT NULL");

        // Process definition analytics
        entity.HasIndex(x => new { x.DeploymentId, x.ProcessBpmnId });

        // ---------- Domain events ----------
        entity.Ignore("DomainEvents"); // or entity.Ignore(x => x.DomainEvents) if accessible
    }
}




