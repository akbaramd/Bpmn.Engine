using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Configurations
{
    public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
    {
        public void Configure(EntityTypeBuilder<Incident> entity)
        {
            // Configure Incident entity
            entity.ToTable("Incidents");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.ProcessId).IsRequired();
            entity.Property(x => x.TokenId);
            entity.Property(x => x.NodeInstanceId);
            entity.Property(x => x.WorkerId);

            entity.Property(x => x.ElementId)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(x => x.Scope)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.Property(x => x.Cause)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(64);

            entity.Property(x => x.ErrorCode).HasMaxLength(500);

            entity.Property(x => x.Message)
                .IsRequired()
                .HasMaxLength(4000);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.Property(x => x.RetryCount).IsRequired();

            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.LastOccurredAtUtc).IsRequired();
            entity.Property(x => x.ResolvedAtUtc);

            // Configure Occurrences as JSON in the database
            entity.Property(x => x.Occurrences)
                .HasConversion(
                    v => JsonConvert.SerializeObject(v), 
                    v => JsonConvert.DeserializeObject<IReadOnlyList<IncidentOccurrence>>(v))
                .HasColumnType("text");

            // Indexes for Incident table for optimized querying
            entity.HasIndex(x => x.ProcessId);
            entity.HasIndex(x => new { x.ProcessId, x.Status, x.LastOccurredAtUtc });
            entity.HasIndex(x => x.NodeInstanceId).HasFilter("[NodeInstanceId] IS NOT NULL");
            entity.HasIndex(x => x.TokenId).HasFilter("[TokenId] IS NOT NULL");
            entity.HasIndex(x => x.WorkerId).HasFilter("[WorkerId] IS NOT NULL");

            // Ignore DomainEvents property, as it's not mapped
            entity.Ignore("DomainEvents");
        }
    }
}
