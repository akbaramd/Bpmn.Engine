using Microsoft.EntityFrameworkCore;
using Novin.Bpmn.Engine.Domain.Entities;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for BPMN Engine
/// Uses in-memory database for development/testing
/// </summary>
public class BpmnEngineDbContext : DbContext
{
    public DbSet<Deployment> Deployments { get; set; }
    public DbSet<Process> Processes { get; set; }
    public DbSet<Token> Tokens { get; set; }
    public DbSet<UserTask> Tasks { get; set; }

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

        modelBuilder.Entity<Process>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ProcessDefinitionId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.State).IsRequired().HasConversion<string>();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.StartedAt);
            entity.Property(e => e.CompletedAt);

            entity.Ignore(e => e.TokenIds);
            entity.Ignore(e => e.Variables);
            entity.Ignore(e => e.DomainEvents);

            // --- _variables (Dictionary) ---
            var varsComparer = new ValueComparer<Dictionary<string, object>>(
                (a, b) => EfComparers.VarsEqual(a, b),
                v => EfComparers.VarsHash(v),
                v => EfComparers.VarsSnapshot(v));

            entity.Property<Dictionary<string, object>>("_variables")
                .HasColumnName("Variables")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null)
                         ?? new Dictionary<string, object>())
                .Metadata.SetValueComparer(varsComparer);

            // --- _tokenIds (HashSet<Guid>) ---
            var tokenIdsComparer = new ValueComparer<HashSet<Guid>>(
                (a, b) => EfComparers.TokenIdsEqual(a, b),
                v => EfComparers.TokenIdsHash(v),
                v => EfComparers.TokenIdsSnapshot(v));

            entity.Property<HashSet<Guid>>("_tokenIds")
                .HasColumnName("TokenIds")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<HashSet<Guid>>(v, (JsonSerializerOptions?)null)
                         ?? new HashSet<Guid>())
                .Metadata.SetValueComparer(tokenIdsComparer);
        });
        // Configure Token
        modelBuilder.Entity<Token>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProcessId).IsRequired();
            entity.Property(e => e.CurrentElementId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.State).IsRequired().HasConversion<string>();

            entity.Property(e => e.IsExecutable).IsRequired();
            entity.Property(e => e.ScopeId);
            entity.Property(e => e.ArrivedViaFlowId).HasMaxLength(500);

            entity.Property(e => e.Variables).HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null)
                     ?? new Dictionary<string, object>());

            entity.Property<List<Guid>>("_parentTokenIds").HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>());

            entity.Ignore(e => e.DomainEvents);
        });

        // Configure Task (UserTask)
        modelBuilder.Entity<UserTask>(entity =>
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
static class EfComparers
{
    public static bool TokenIdsEqual(HashSet<Guid>? a, HashSet<Guid>? b)
        => ReferenceEquals(a, b) || (a is not null && b is not null && a.SetEquals(b));

    public static int TokenIdsHash(HashSet<Guid>? v)
    {
        if (v is null || v.Count == 0) return 0;

        var hash = 0;
        foreach (var g in v.OrderBy(x => x))
            hash = HashCode.Combine(hash, g.GetHashCode());

        return hash;
    }

    public static HashSet<Guid> TokenIdsSnapshot(HashSet<Guid>? v)
        => v is null ? new HashSet<Guid>() : new HashSet<Guid>(v);

    public static bool VarsEqual(Dictionary<string, object>? a, Dictionary<string, object>? b)
        => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null)
           == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null);

    public static int VarsHash(Dictionary<string, object>? v)
        => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode();

    public static Dictionary<string, object> VarsSnapshot(Dictionary<string, object>? v)
        => v is null ? new Dictionary<string, object>() : new Dictionary<string, object>(v);
}