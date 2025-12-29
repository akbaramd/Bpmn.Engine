using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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

        // optimistic concurrency (your domain increments Version)
        entity.Property(x => x.Version)
            .IsConcurrencyToken();

        entity.Property(x => x.CreatedAtUtc).IsRequired();
        entity.Property(x => x.TriggeredAtUtc);
        entity.Property(x => x.CanceledAtUtc);

        // Indexes
        entity.HasIndex(x => x.ProcessId);
        entity.HasIndex(x => x.TokenId);
        entity.HasIndex(x => x.NodeInstanceId);

        entity.HasIndex(x => new { x.ProcessId, x.State, x.Kind });
        entity.HasIndex(x => new { x.State, x.DueAt })
            .HasFilter("[DueAt] IS NOT NULL");

        entity.HasIndex(x => new { x.ProcessId, x.HostElementId });
        entity.HasIndex(x => new { x.ProcessId, x.BoundaryElementId });

        entity.HasIndex(x => new { x.ProcessId, x.ActivityInstanceId })
            .HasFilter("[ActivityInstanceId] IS NOT NULL");

        entity.HasIndex(x => new { x.ProcessId, x.TokenScopeId })
            .HasFilter("[TokenScopeId] IS NOT NULL");

        entity.Ignore("DomainEvents");
    }
}