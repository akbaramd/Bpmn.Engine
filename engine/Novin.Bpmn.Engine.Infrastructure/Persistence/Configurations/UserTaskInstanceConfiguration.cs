using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Configurations;

public sealed class UserTaskInstanceConfiguration : IEntityTypeConfiguration<UserTaskInstance>
{
    public void Configure(EntityTypeBuilder<UserTaskInstance> entity)
    {
        entity.ToTable("UserTasks");

        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();

        // --------------------
        // Correlation
        // --------------------
        entity.Property(x => x.ProcessId).IsRequired();
        entity.Property(x => x.TokenId).IsRequired();
        entity.Property(x => x.NodeInstanceId);

        entity.Property(x => x.ElementId)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(x => x.TaskName)
            .IsRequired()
            .HasMaxLength(1000);

        // --------------------
        // State
        // --------------------
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

        // --------------------
        // Variables (SINGLE JSON BLOB)
        // Domain:
        //   private string _variablesJson = "{}";
        //   public string VariablesJson => _variablesJson;
        //   public JsonObject VariablesObject => ...
        // --------------------
        // If you have VariablesObject / VariablesJson exposed publicly, ignore them for EF mapping.
        // (Same approach as ProcessConfiguration)
        entity.Ignore("VariablesObject"); // safe even if property exists; else remove this line
        entity.Property<string>("_variablesJ")
            .HasColumnName("VariablesJson")
            .HasColumnType("text")
            .IsRequired();

        // --------------------
        // Metadata (SINGLE JSON BLOB)
        // Domain:
        //   private string _metadataJson = "{}";
        //   public string MetadataJson => _metadataJson;
        //   public JsonObject MetadataObject => ...
        // --------------------
        entity.Ignore("MetadataObject"); // safe even if property exists; else remove this line
        entity.Property<string>("_metadata")
            .HasColumnName("MetadataJson")
            .HasColumnType("text")
            .IsRequired();

        // --------------------
        // Indexes
        // --------------------
        entity.HasIndex(x => x.ProcessId);
        entity.HasIndex(x => x.TokenId);
        entity.HasIndex(x => x.NodeInstanceId);

        entity.HasIndex(x => new { x.ProcessId, x.Status, x.CreatedAtUtc });
        entity.HasIndex(x => new { x.Status, x.CreatedAtUtc });

        // Inbox-style queries
        entity.HasIndex(x => x.ClaimedByUserId);
        entity.HasIndex(x => x.CompletedByUserId);

        // Domain events
        entity.Ignore("DomainEvents");
    }
}
