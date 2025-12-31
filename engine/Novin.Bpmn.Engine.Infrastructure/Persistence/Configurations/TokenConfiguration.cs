using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Infrastructure.Common;
using Novin.Bpmn.Engine.Infrastructure.Persistence.Ef;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Configurations;

public sealed class TokenConfiguration : IEntityTypeConfiguration<Token>
{
    public void Configure(EntityTypeBuilder<Token> entity)
    {
        entity.ToTable("Tokens");

        entity.HasKey(x => x.Id);

        // -------- Correlation --------
        entity.Property(x => x.ProcessId).IsRequired();

        entity.Property(x => x.CurrentElementId)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(x => x.State)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);


        entity.Property(x => x.IsExecutable).IsRequired();

        entity.Property(x => x.ScopeId);
        entity.Property(x => x.ActivityInstanceId);

        // ArrivedViaFlowIds (private _arrivedViaFlowIds) - stored as JSON
        entity.Ignore(x => x.ArrivedViaFlowIds);

        var flowIdsComparer = new ValueComparer<List<string>>(
            (a, b) => EfComparers.ListEqual(a, b),
            v => EfComparers.ListHash(v),
            v => EfComparers.ListSnapshot(v));

        entity.Property<List<string>>("_arrivedViaFlowIds")
            .HasColumnName("ArrivedViaFlowIds")
            .HasColumnType("text")
            .HasConversion(
                v => JsonHelper.SerializeObject(v ?? new List<string>()),
                v => JsonHelper.DeserializeObject<List<string>>(v) ?? new List<string>())
            .Metadata.SetValueComparer(flowIdsComparer);

        // -------- ParentTokenIds (private List<Guid> _parentTokenIds -> JSON) --------
        entity.Ignore(x => x.ParentTokenIds);

        var parentComparer = new ValueComparer<List<Guid>>(
            (a, b) => EfComparers.GuidListEqual(a, b),
            v => EfComparers.GuidListHash(v),
            v => EfComparers.GuidListSnapshot(v));

        entity.Property<List<Guid>>("_parentTokenIds")
            .HasColumnName("ParentTokenIds")
            .HasColumnType("text")
            .HasConversion(
                v => JsonHelper.SerializeObject(v ?? new List<Guid>()),
                v => JsonHelper.DeserializeObject<List<Guid>>(v) ?? new List<Guid>())
            .Metadata.SetValueComparer(parentComparer);

        // -------- Local Variables (private Dictionary<string,string> _variables -> JSON) --------
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

        // -------- Timeline --------
        entity.Property(x => x.CreatedAt).IsRequired();
        entity.Property(x => x.ActivatedAt);
        entity.Property(x => x.CompletedAt);

        // -------- Indexes --------
        entity.HasIndex(x => x.ProcessId);

        // Useful for engine polling / visualization
        entity.HasIndex(x => new { x.ProcessId, x.State });
        entity.HasIndex(x => new { x.ProcessId, x.CurrentElementId });

        // Correlation for fork/join
        entity.HasIndex(x => new { x.ProcessId, x.ScopeId })
            .HasFilter("[ScopeId] IS NOT NULL");

        // Activity instance correlation (boundary cancel, subprocess)
        entity.HasIndex(x => new { x.ProcessId, x.ActivityInstanceId })
            .HasFilter("[ActivityInstanceId] IS NOT NULL");

        // -------- Domain events --------
        entity.Ignore("DomainEvents");
    }
}