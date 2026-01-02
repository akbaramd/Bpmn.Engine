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

        // If Id is generated in domain (recommended for AggregateRoot)
        entity.Property(x => x.Id).ValueGeneratedNever();

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

        entity.Property(x => x.FailureReason).HasMaxLength(2000);
        entity.Property(x => x.TerminationReason).HasMaxLength(2000);

        // ---------- Domain collections (IDs only) ----------
        entity.Ignore(x => x.TokenIds);
        entity.Ignore(x => x.NodeInstanceIds);

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

        var dictComparer = new ValueComparer<Dictionary<string, string>>(
            (a, b) => EfComparers.VarsEqual(a, b),
            v => EfComparers.VarsHash(v),
            v => EfComparers.VarsSnapshot(v));

        entity.Property<Dictionary<string, string>>("_variables")
            .HasColumnName("Variables")
            .HasColumnType("text")
            .HasConversion(
                v => JsonHelper.SerializeObject(v ?? new Dictionary<string, string>(StringComparer.Ordinal)),
                v => JsonHelper.DeserializeObject<Dictionary<string, string>>(v) ?? new Dictionary<string, string>(StringComparer.Ordinal))
            .Metadata.SetValueComparer(dictComparer);

        // ---------- Metadata (Dictionary<string,string>) ----------
        // Requires you added:
        //   private readonly Dictionary<string,string> _metadata = new(StringComparer.Ordinal);
        //   public IReadOnlyDictionary<string,string> Metadata => _metadata;
        entity.Ignore(x => x.Metadata);

        entity.Property<Dictionary<string, string>>("_metadata")
            .HasColumnName("Metadata")
            .HasColumnType("text")
            .HasConversion(
                v => JsonHelper.SerializeObject(v ?? new Dictionary<string, string>(StringComparer.Ordinal)),
                v => JsonHelper.DeserializeObject<Dictionary<string, string>>(v) ?? new Dictionary<string, string>(StringComparer.Ordinal))
            .Metadata.SetValueComparer(dictComparer);

        // ---------- Indexes ----------
        entity.HasIndex(x => x.ProjectId);
        entity.HasIndex(x => x.DeploymentId);
        entity.HasIndex(x => new { x.ProjectId, x.State });
        entity.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });

        // NOTE: HasFilter is provider-specific:
        // - SQL Server: "[BusinessKey] IS NOT NULL"
        // - PostgreSQL (Npgsql): "\"BusinessKey\" IS NOT NULL"
        // If you want to stay provider-agnostic, remove HasFilter.
        entity.HasIndex(x => new { x.ProjectId, x.BusinessKey })
            .HasFilter("[BusinessKey] IS NOT NULL");

        entity.HasIndex(x => new { x.DeploymentId, x.ProcessBpmnId });

        // ---------- Domain events ----------
        entity.Ignore("DomainEvents");
    }
}
