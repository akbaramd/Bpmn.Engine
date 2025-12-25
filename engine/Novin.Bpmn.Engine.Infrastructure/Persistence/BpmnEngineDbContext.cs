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

    public DbSet<Incident> Incidents { get; set; }
    public DbSet<BoundarySubscription> BoundarySubscriptions { get; set; }
    public BpmnEngineDbContext(DbContextOptions<BpmnEngineDbContext> options) : base(options)
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
            // Process._variables is Dictionary<string, object> (private field)
            // But Process.Variables property is IReadOnlyDictionary<string, object>
            // Since we're mapping the private field _variables, Dictionary comparer is correct
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
            entity.Property(e => e.ActivityInstanceId);
            entity.Property(e => e.ArrivedViaFlowId).HasMaxLength(500);

            // Token.Variables is IReadOnlyDictionary<string, object>
            // Need ValueComparer<IReadOnlyDictionary<string, object>>, not Dictionary
            var tokenVarsComparer = new ValueComparer<IReadOnlyDictionary<string, object>>(
                (a, b) => EfComparers.ReadOnlyDictEqual(a, b),
                v => EfComparers.ReadOnlyDictHash(v),
                v => EfComparers.ReadOnlyDictSnapshot(v));

            entity.Property(e => e.Variables).HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null)
                         ?? new Dictionary<string, object>())
                .Metadata.SetValueComparer(tokenVarsComparer);

            var parentTokenIdsComparer = new ValueComparer<List<Guid>>(
                (a, b) => EfComparers.ParentTokenIdsEqual(a, b),
                v => EfComparers.ParentTokenIdsHash(v),
                v => EfComparers.ParentTokenIdsSnapshot(v));

            entity.Property<List<Guid>>("_parentTokenIds")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>())
                .Metadata.SetValueComparer(parentTokenIdsComparer);

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
            // UserTask.InputVariables and OutputVariables are Dictionary<string, object>
            var userTaskVarsComparer = new ValueComparer<Dictionary<string, object>>(
                (a, b) => EfComparers.VarsEqual(a, b),
                v => EfComparers.VarsHash(v),
                v => EfComparers.VarsSnapshot(v));

            entity.Property(e => e.InputVariables)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>())
                .Metadata.SetValueComparer(userTaskVarsComparer);

            entity.Property(e => e.OutputVariables)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>())
                .Metadata.SetValueComparer(userTaskVarsComparer);

            // Ignore domain events
            entity.Ignore(e => e.DomainEvents);
        });

        // Configure Incident
        modelBuilder.Entity<Incident>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProcessId).IsRequired();
            entity.Property(e => e.TokenId).IsRequired();
            entity.Property(e => e.ElementId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Type).IsRequired().HasConversion<string>();
            entity.Property(e => e.ErrorCode).HasMaxLength(500);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.StackTrace).HasMaxLength(10000);
            entity.Property(e => e.Status).IsRequired().HasConversion<string>();
            entity.Property(e => e.Retries).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.LastOccurredAt).IsRequired();
            entity.Property(e => e.ResolvedAt);
        });

        // Configure BoundarySubscription
        modelBuilder.Entity<BoundarySubscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProcessId).IsRequired();
            entity.Property(e => e.TokenId).IsRequired();
            entity.Property(e => e.ActivityInstanceId);
            entity.Property(e => e.AttachedToElementId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.BoundaryEventId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Kind).IsRequired().HasConversion<string>();
            entity.Property(e => e.IsInterrupting).IsRequired();
            entity.Property(e => e.State).IsRequired().HasConversion<string>();
            entity.Property(e => e.DueAt);
            entity.Property(e => e.ExternalJobKey).HasMaxLength(500);
            entity.Property(e => e.CorrelationKey).HasMaxLength(500);
            entity.Property(e => e.ErrorCode).HasMaxLength(500);
            entity.Property(e => e.Version).IsConcurrencyToken();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.TriggeredAt);
            entity.Property(e => e.CanceledAt);

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

    // Comparers for IReadOnlyDictionary<string, object> (used by Token.Variables)
    public static bool ReadOnlyDictEqual(IReadOnlyDictionary<string, object>? a, IReadOnlyDictionary<string, object>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;

        return JsonSerializer.Serialize(a, (JsonSerializerOptions?)null)
               == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null);
    }

    public static int ReadOnlyDictHash(IReadOnlyDictionary<string, object>? v)
        => v == null ? 0 : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode();

    public static IReadOnlyDictionary<string, object> ReadOnlyDictSnapshot(IReadOnlyDictionary<string, object>? v)
    {
        if (v == null) return new Dictionary<string, object>();
        
        // Convert to Dictionary for snapshot (EF Core needs mutable snapshot)
        return new Dictionary<string, object>(v);
    }

    // Comparers for List<Guid> (used by Token._parentTokenIds)
    public static bool ParentTokenIdsEqual(List<Guid>? a, List<Guid>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;
        return a.SequenceEqual(b);
    }

    public static int ParentTokenIdsHash(List<Guid>? v)
    {
        if (v == null || v.Count == 0) return 0;
        var hash = 0;
        foreach (var g in v)
            hash = HashCode.Combine(hash, g.GetHashCode());
        return hash;
    }

    public static List<Guid> ParentTokenIdsSnapshot(List<Guid>? v)
        => v == null ? new List<Guid>() : new List<Guid>(v);
}