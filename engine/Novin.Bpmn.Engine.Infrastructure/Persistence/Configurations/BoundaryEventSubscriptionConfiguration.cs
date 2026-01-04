using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Engine.Infrastructure.Common;
using Novin.Bpmn.Engine.Infrastructure.Persistence.Ef;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Configurations;

public sealed class BoundaryEventSubscriptionConfiguration : IEntityTypeConfiguration<BoundaryEventSubscription>
{
    public void Configure(EntityTypeBuilder<BoundaryEventSubscription> entity)
    {
        entity.ToTable("BoundaryEventSubscriptions");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.ProcessId).IsRequired();
        entity.Property(x => x.TokenId).IsRequired();
        entity.Property(x => x.NodeInstanceId);

        entity.Property(x => x.HostElementId)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(x => x.BoundaryElementId)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(x => x.Kind)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        entity.Property(x => x.IsInterrupting).IsRequired();

        entity.Property(x => x.State)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        entity.Property(x => x.DueAt);
        entity.Property(x => x.ExternalJobKey).HasMaxLength(500);

        entity.Property(x => x.CorrelationKey).HasMaxLength(500);
        entity.Property(x => x.ErrorCode).HasMaxLength(500);

        entity.Property(x => x.ActivityInstanceId);
        entity.Property(x => x.TokenScopeId);

        // Timer-specific fields
        entity.Property(x => x.TimerType)
            .HasConversion<string>()
            .HasMaxLength(32);
        entity.Property(x => x.TimerExpression).HasMaxLength(1000);
        entity.Property(x => x.NextDueAtUtc);
        entity.Property(x => x.LastFiredAtUtc);
        entity.Property(x => x.FireCount).IsRequired().HasDefaultValue(0);

        // optimistic concurrency (your domain increments Version)
        entity.Property(x => x.Version)
            .IsConcurrencyToken();

        entity.Property(x => x.CreatedAtUtc).IsRequired();
        entity.Property(x => x.TriggeredAtUtc);
        entity.Property(x => x.CanceledAtUtc);

        // Meta: JSON storage for Tracing/Debug/UI data (non-hot-path)
        var metaComparer = new ValueComparer<MetaBag>(
            (a, b) => ReferenceEquals(a, b) || (a != null && b != null && EfComparers.ReadOnlyDictEqual(a.Values, b.Values)) || (a == null && b == null),
            v => v == null ? 0 : EfComparers.ReadOnlyDictHash(v.Values),
            v => v == null 
                ? MetaBag.Empty 
                : MetaBag.From(v.Values));

        entity.Property(x => x.Meta)
            .HasColumnType("text")
            .HasConversion(
                v => v == null 
                    ? JsonHelper.SerializeObject(new Dictionary<string, string>())
                    : JsonHelper.SerializeObject(v.Values ?? new Dictionary<string, string>()),
                v => string.IsNullOrWhiteSpace(v)
                    ? MetaBag.Empty
                    : MetaBag.From(JsonHelper.DeserializeObject<Dictionary<string, string>>(v)))
            .Metadata.SetValueComparer(metaComparer);

        // Indexes
        entity.HasIndex(x => x.ProcessId);
        entity.HasIndex(x => x.TokenId);
        entity.HasIndex(x => x.NodeInstanceId);

     entity.HasIndex(x => new { x.State, x.DueAt })
    .HasFilter("\"DueAt\" IS NOT NULL");

entity.HasIndex(x => new { x.ProcessId, x.ActivityInstanceId })
    .HasFilter("\"ActivityInstanceId\" IS NOT NULL");

entity.HasIndex(x => new { x.ProcessId, x.TokenScopeId })
    .HasFilter("\"TokenScopeId\" IS NOT NULL");

// Timer indexes for recovery (Kind/State are strings)
entity.HasIndex(x => new { x.Kind, x.State, x.DueAt })
    .HasFilter("\"Kind\" = 'Timer' AND \"State\" = 'Active' AND \"DueAt\" IS NOT NULL");

entity.HasIndex(x => new { x.Kind, x.State, x.NextDueAtUtc })
    .HasFilter("\"Kind\" = 'Timer' AND \"State\" = 'Active' AND \"NextDueAtUtc\" IS NOT NULL");

        entity.Ignore("DomainEvents");
    }
}