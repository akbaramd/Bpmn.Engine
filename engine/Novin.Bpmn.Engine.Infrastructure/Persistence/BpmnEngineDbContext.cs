using Microsoft.EntityFrameworkCore;
using Novin.Bpmn.Engine.Domain.Entities;
using System.Text.Json;
using Task = Novin.Bpmn.Engine.Domain.Entities.Task;


namespace Novin.Bpmn.Engine.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for BPMN Engine
/// Uses in-memory database for development/testing
/// </summary>
public class BpmnEngineDbContext : DbContext
{
    public DbSet<Deployment> Deployments { get; set; }
    public DbSet<Process> Processes { get; set; }
    public DbSet<Node> Nodes { get; set; }
    public DbSet<Token> Tokens { get; set; }
    public DbSet<Task> Tasks { get; set; }
    public DbSet<ProcessHistory> ProcessHistories { get; set; }
    public DbSet<TokenHistoryEntry> TokenHistoryEntries { get; set; }
    public DbSet<NodeTokenHistoryEntry> NodeTokenHistoryEntries { get; set; }

    public BpmnEngineDbContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Deployment
        modelBuilder.Entity<Deployment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DeploymentKey).IsRequired().HasMaxLength(500);
            entity.Property(e => e.BpmnXml).IsRequired();
            entity.Property(e => e.Label).HasMaxLength(1000);
            entity.Property(e => e.DeployedAt).IsRequired();
            entity.Property(e => e.Version).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();

            // Ignore domain events (they're not persisted)
            entity.Ignore(e => e.DomainEvents);
        });

        // Configure Process
        modelBuilder.Entity<Process>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ProcessDefinitionId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.State).IsRequired().HasConversion<string>();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.StartedAt);
            entity.Property(e => e.CompletedAt);

            // Store Variables as JSON
            entity.Property(e => e.Variables)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>());

            // Store collections as JSON using backing fields
            entity.Property<List<Guid>>("_tokenIds")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>());

            // ProcessHistory as owned collection (stored in separate table)
            entity.OwnsMany(e => e.History, owned =>
            {
                owned.WithOwner().HasForeignKey("ProcessId");
                owned.Property(h => h.ProcessId).IsRequired();
                owned.Property(h => h.NodeId).IsRequired();
                owned.Property(h => h.ElementId).IsRequired().HasMaxLength(500);
                owned.Property(h => h.NodeName).IsRequired().HasMaxLength(500);
                owned.Property(h => h.State).IsRequired().HasConversion<string>();
                owned.Property(h => h.TokenId);
                owned.Property(h => h.ExecutedAt).IsRequired();
            });

            // Ignore domain events
            entity.Ignore(e => e.DomainEvents);
        });

        // Configure Node
        modelBuilder.Entity<Node>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProcessId).IsRequired();
            entity.Property(e => e.NodeName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ElementId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Type).IsRequired().HasConversion<string>();
            entity.Property(e => e.State).IsRequired().HasConversion<string>();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ProcessingStartedAt);
            entity.Property(e => e.CompletedAt);
            entity.Property(e => e.FailedAt);
            entity.Property(e => e.ErrorCode).HasMaxLength(200);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);

            // Store Variables as JSON
            entity.Property(e => e.Variables)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>());

            // Store collections as JSON using backing fields
            entity.Property<List<Guid>>("_currentTokenIds")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>());

            entity.Property<List<Guid>>("_parentNodeIds")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>());

            entity.Property<List<Guid>>("_childNodeIds")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>());

            entity.Property<List<string>>("_history")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

            // NodeTokenHistoryEntry as owned collection
            entity.OwnsMany(e => e.TokenHistory, owned =>
            {
                owned.WithOwner().HasForeignKey("NodeId");
                owned.Property(h => h.NodeId).IsRequired();
                owned.Property(h => h.TokenId).IsRequired();
                owned.Property(h => h.ElementId).IsRequired().HasMaxLength(500);
                owned.Property(h => h.ReachedAt).IsRequired();
                owned.Property(h => h.CompletedAt);
                owned.Property(h => h.OutputVariables)
                    .HasConversion(
                        v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null));
            });

            // Ignore domain events
            entity.Ignore(e => e.DomainEvents);
        });

        // Configure Token
        modelBuilder.Entity<Token>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProcessId).IsRequired();
            entity.Property(e => e.CurrentElementId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CurrentNodeId);
            entity.Property(e => e.State).IsRequired().HasConversion<string>();
            entity.Property(e => e.ParentTokenId);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ActivatedAt);
            entity.Property(e => e.CompletedAt);

            // Store collections as JSON using backing fields
            entity.Property<List<Guid>>("_parentNodeIds")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>());

            entity.Property<List<string>>("_nextNodes")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

            entity.Property<List<string>>("_history")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

            // TokenHistoryEntry as owned collection
            entity.OwnsMany(e => e.TokenHistory, owned =>
            {
                owned.WithOwner().HasForeignKey("TokenId");
                owned.Property(h => h.TokenId).IsRequired();
                owned.Property(h => h.NodeId).IsRequired();
                owned.Property(h => h.ElementId).IsRequired().HasMaxLength(500);
                owned.Property(h => h.NodeName).IsRequired().HasMaxLength(500);
                owned.Property(h => h.ReachedAt).IsRequired();
                owned.Property(h => h.LeftAt);
                owned.Property(h => h.State).IsRequired().HasConversion<string>();
                owned.Property(h => h.Variables)
                    .HasConversion(
                        v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null));
            });

            // Ignore domain events
            entity.Ignore(e => e.DomainEvents);
        });

        // Configure Task
        modelBuilder.Entity<Task>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProcessId).IsRequired();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ElementId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Status).IsRequired().HasConversion<string>();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.StartedAt);
            entity.Property(e => e.CompletedAt);
            entity.Property(e => e.AssignedTo).HasMaxLength(500);

            // Store Variables as JSON
            entity.Property(e => e.InputVariables)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>());

            entity.Property(e => e.OutputVariables)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>());

            // Ignore domain events
            entity.Ignore(e => e.DomainEvents);
        });

     
  
    }
}

