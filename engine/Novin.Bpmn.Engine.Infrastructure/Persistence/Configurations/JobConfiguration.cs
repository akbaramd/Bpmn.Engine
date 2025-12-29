using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Novin.Bpmn.Engine.Infrastructure.Common;
using Novin.Bpmn.Engine.Infrastructure.Persistence.Ef;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Configurations.Novin.Bpmn.Engine.Infrastructure.Persistence.Configurations;

public sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> entity)
    {
        entity.ToTable("Jobs");

        entity.HasKey(x => x.Id);

        // -------- Concurrency (optional but recommended) --------
        entity.Property<byte[]>("RowVersion")
            .IsRowVersion()
            .IsConcurrencyToken();

        // -------- Correlation --------
        entity.Property(x => x.ProcessId).IsRequired();
        entity.Property(x => x.TokenId).IsRequired();
        entity.Property(x => x.NodeInstanceId);

        entity.Property(x => x.ElementId)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(x => x.TaskName)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(x => x.Implementation)
            .IsRequired()
            .HasMaxLength(1000);

        // -------- Status / Attempts --------
        entity.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        entity.Property(x => x.Attempts).IsRequired();

        // -------- Timeline --------
        entity.Property(x => x.CreatedAtUtc).IsRequired();
        entity.Property(x => x.LeasedAtUtc);
        entity.Property(x => x.StartedAtUtc);
        entity.Property(x => x.CompletedAtUtc);
        entity.Property(x => x.NextAttemptAtUtc);

        // -------- Lease / Lock --------
        entity.Property(x => x.ClientId).HasMaxLength(256);
        entity.Property(x => x.LockId).HasMaxLength(256);
        entity.Property(x => x.LockedUntilUtc);

        // -------- Error --------
        entity.Property(x => x.ErrorMessage).HasMaxLength(4000);

        // -------- Payload/Result JSON --------
        // If SQL Server: prefer nvarchar(max); for PostgreSQL use jsonb.
        var dictComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<string, string>>(
            (a, b) => EfComparers.VarsEqual(a, b),
            v => EfComparers.VarsHash(v),
            v => EfComparers.VarsSnapshot(v));

        entity.Property(x => x.Payload)
            .IsRequired()
            .HasColumnType("text")
            .HasConversion(
                v => JsonHelper.SerializeObject(v ?? new Dictionary<string, string>(StringComparer.Ordinal)),
                v => JsonHelper.DeserializeObject<Dictionary<string, string>>(v) ?? new Dictionary<string, string>(StringComparer.Ordinal))
            .Metadata.SetValueComparer(dictComparer);

        entity.Property(x => x.Result)
            .IsRequired()
            .HasColumnType("text")
            .HasConversion(
                v => JsonHelper.SerializeObject(v ?? new Dictionary<string, string>(StringComparer.Ordinal)),
                v => JsonHelper.DeserializeObject<Dictionary<string, string>>(v) ?? new Dictionary<string, string>(StringComparer.Ordinal))
            .Metadata.SetValueComparer(dictComparer);

        // -------- Indexes --------
        entity.HasIndex(x => x.ProcessId);
        entity.HasIndex(x => x.TokenId);
        entity.HasIndex(x => x.NodeInstanceId);

        entity.HasIndex(x => new { x.ProcessId, x.Status });
        entity.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        entity.HasIndex(x => new { x.Status, x.LockedUntilUtc });

        entity.HasIndex(x => x.CreatedAtUtc);
        entity.HasIndex(x => x.CompletedAtUtc);

        entity.HasIndex(x => x.ClientId);

        // -------- Domain events --------
        entity.Ignore("DomainEvents");
    }
}