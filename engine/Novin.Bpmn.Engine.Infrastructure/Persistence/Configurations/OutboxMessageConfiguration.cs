using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> entity)
    {
        entity.ToTable("OutboxMessages");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.OccurredOnUtc).IsRequired();

        entity.Property(x => x.MessageName)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(x => x.MessageType)
            .HasMaxLength(400);

        entity.Property(x => x.Payload)
            .IsRequired();

        entity.Property(x => x.Status)
            .IsRequired()
            .HasConversion<byte>();

        entity.Property(x => x.Attempts).IsRequired();

        entity.Property(x => x.NextAttemptOnUtc);
        entity.Property(x => x.ProcessedOnUtc);

        entity.Property(x => x.LockId);
        entity.Property(x => x.LockedUntilUtc);

        entity.Property(x => x.LastError);

        entity.Property(x => x.CorrelationId);
        entity.Property(x => x.PartitionKey).HasMaxLength(100);
        entity.Property(x => x.AggregateId);

// Indexes (PostgreSQL partial indexes)
entity.HasIndex(e => new { e.Status, e.OccurredOnUtc, e.NextAttemptOnUtc })
    .HasFilter("\"Status\" IN (0, 3)")
    .HasDatabaseName("IX_OutboxMessages_Status_Occurred_NextAttempt");

entity.HasIndex(e => new { e.Status, e.LockedUntilUtc })
    .HasFilter("\"Status\" = 1 AND \"LockedUntilUtc\" IS NOT NULL")
    .HasDatabaseName("IX_OutboxMessages_Status_LockedUntil");

entity.HasIndex(e => new { e.PartitionKey, e.Status, e.OccurredOnUtc })
    .HasFilter("\"PartitionKey\" IS NOT NULL")
    .HasDatabaseName("IX_OutboxMessages_PartitionKey_Status_Occurred");

entity.HasIndex(e => e.CorrelationId)
    .HasFilter("\"CorrelationId\" IS NOT NULL")
    .HasDatabaseName("IX_OutboxMessages_CorrelationId");

entity.HasIndex(e => e.AggregateId)
    .HasFilter("\"AggregateId\" IS NOT NULL")
    .HasDatabaseName("IX_OutboxMessages_AggregateId");

    }
}