using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Infrastructure.Common;
using Novin.Bpmn.Engine.Infrastructure.Persistence.Ef;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Configurations;

public sealed class NodeInstanceConfiguration : IEntityTypeConfiguration<NodeInstance>
{
    public void Configure(EntityTypeBuilder<NodeInstance> entity)
    {
        entity.ToTable("NodeInstances");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.ProcessId).IsRequired();
        entity.Property(x => x.TokenId).IsRequired();

        entity.Property(x => x.ElementId)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(x => x.State)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        entity.Property(x => x.CreatedAtUtc).IsRequired();
        entity.Property(x => x.StartedAtUtc);
        entity.Property(x => x.CompletedAtUtc);

        entity.Property(x => x.ScopeId);
        entity.Property(x => x.ActivityInstanceId);

        entity.Property(x => x.ArrivedViaFlowId)
            .HasMaxLength(500);

        entity.Property(x => x.WorkerId);
        entity.Property(x => x.ErrorMessage).HasMaxLength(4000);

        // Variables (private _variables)
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

        // Indexes
        entity.HasIndex(x => x.ProcessId);
        entity.HasIndex(x => x.TokenId);
        entity.HasIndex(x => new { x.ProcessId, x.ElementId });
        entity.HasIndex(x => new { x.ProcessId, x.State, x.CreatedAtUtc });

        entity.HasIndex(x => new { x.ProcessId, x.ScopeId })
            .HasFilter("[ScopeId] IS NOT NULL");

        entity.HasIndex(x => new { x.ProcessId, x.ActivityInstanceId })
            .HasFilter("[ActivityInstanceId] IS NOT NULL");

        entity.HasIndex(x => x.WorkerId)
            .HasFilter("[WorkerId] IS NOT NULL");

        entity.Ignore("DomainEvents");
    }
}