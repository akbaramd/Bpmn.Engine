using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Infrastructure.Common;
using Novin.Bpmn.Engine.Infrastructure.Persistence.Ef;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Configurations;

public sealed class UserTaskInstanceConfiguration : IEntityTypeConfiguration<UserTaskInstance>
{
    public void Configure(EntityTypeBuilder<UserTaskInstance> entity)
    {
        entity.ToTable("UserTasks");

        entity.HasKey(x => x.Id);

        // Correlation
        entity.Property(x => x.ProcessId).IsRequired();
        entity.Property(x => x.TokenId).IsRequired();
        entity.Property(x => x.NodeInstanceId);

        entity.Property(x => x.ElementId)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(x => x.TaskName)
            .IsRequired()
            .HasMaxLength(1000);

        // State
        entity.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        entity.Property(x => x.CreatedAtUtc).IsRequired();
        entity.Property(x => x.ClaimedAtUtc);
        entity.Property(x => x.StartedAtUtc);
        entity.Property(x => x.CompletedAtUtc);
        entity.Property(x => x.CanceledAtUtc);

        entity.Property(x => x.ClaimedByUserId).HasMaxLength(256);
        entity.Property(x => x.CompletedByUserId).HasMaxLength(256);
        entity.Property(x => x.CancelReason).HasMaxLength(2000);

        // JSON dictionaries
        var dictComparer = new ValueComparer<Dictionary<string, string>>(
            (a, b) => EfComparers.VarsEqual(a, b),
            v => EfComparers.VarsHash(v),
            v => EfComparers.VarsSnapshot(v));

        entity.Property(x => x.Metadata)
            .IsRequired()
            .HasColumnType("text")
            .HasConversion(
                v => JsonHelper.SerializeObject(v ?? new Dictionary<string, string>(StringComparer.Ordinal)),
                v => JsonHelper.DeserializeObject<Dictionary<string, string>>(v) ?? new Dictionary<string, string>(StringComparer.Ordinal))
            .Metadata.SetValueComparer(dictComparer);

        entity.Property(x => x.Variables)
            .IsRequired()
            .HasColumnType("text")
            .HasConversion(
                v => JsonHelper.SerializeObject(v ?? new Dictionary<string, string>(StringComparer.Ordinal)),
                v => JsonHelper.DeserializeObject<Dictionary<string, string>>(v) ?? new Dictionary<string, string>(StringComparer.Ordinal))
            .Metadata.SetValueComparer(dictComparer);

        // Indexes
        entity.HasIndex(x => x.ProcessId);
        entity.HasIndex(x => x.TokenId);
        entity.HasIndex(x => x.NodeInstanceId);

        entity.HasIndex(x => new { x.ProcessId, x.Status, x.CreatedAtUtc });
        entity.HasIndex(x => new { x.Status, x.CreatedAtUtc });

        // Inbox-style queries
        entity.HasIndex(x => x.ClaimedByUserId);
        entity.HasIndex(x => x.CompletedByUserId);

        entity.Ignore("DomainEvents");
    }
}