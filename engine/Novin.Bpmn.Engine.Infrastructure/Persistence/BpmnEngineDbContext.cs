using Microsoft.EntityFrameworkCore;
using Novin.Bpmn.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Novin.Bpmn.Engine.Infrastructure.Common;

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

    public DbSet<Incident> Incidents { get; set; }
    public DbSet<BoundarySubscription> BoundarySubscriptions { get; set; }
    public DbSet<ExecutedNode> ProcessExecutionNodes { get; set; }
    public DbSet<Worker> Workers { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    
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
            entity.Property(e => e.DeploymentId).IsRequired();
            entity.Property(e => e.ProcessBpmnId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.State).IsRequired().HasConversion<string>();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.StartedAt);
            entity.Property(e => e.CompletedAt);

            entity.Ignore(e => e.TokenIds);
            entity.Ignore(e => e.Variables);
            entity.Ignore(e => e.DomainEvents);
            entity.Ignore(e => e.ExecutionNodes); // ExecutionNodes are managed separately via ExecutedNode entities

            // --- _variables (Dictionary) ---
            // Process._variables is Dictionary<string, string> (private field)
            // But Process.Variables property is IReadOnlyDictionary<string, string>
            // Since we're mapping the private field _variables, Dictionary comparer is correct
            var varsComparer = new ValueComparer<Dictionary<string, string>>(
                (a, b) => EfComparers.VarsEqual(a, b),
                v => EfComparers.VarsHash(v),
                v => EfComparers.VarsSnapshot(v));

            entity.Property<Dictionary<string, string>>("_variables")
                .HasColumnName("Variables")
                .HasConversion(
                    v => JsonHelper.SerializeObject(v),
                    v => JsonHelper.DeserializeObject<Dictionary<string, string>>(v)
                         ?? new Dictionary<string, string>())
                .Metadata.SetValueComparer(varsComparer);

            // --- _tokenIds (HashSet<Guid>) ---
            var tokenIdsComparer = new ValueComparer<HashSet<Guid>>(
                (a, b) => EfComparers.TokenIdsEqual(a, b),
                v => EfComparers.TokenIdsHash(v),
                v => EfComparers.TokenIdsSnapshot(v));

            entity.Property<HashSet<Guid>>("_tokenIds")
                .HasColumnName("TokenIds")
                .HasConversion(
                    v => JsonHelper.SerializeObject(v),
                    v => JsonHelper.DeserializeObject<HashSet<Guid>>(v)
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

            // Token.Variables is IReadOnlyDictionary<string, string>
            // Need ValueComparer<IReadOnlyDictionary<string, string>>, not Dictionary
            var tokenVarsComparer = new ValueComparer<IReadOnlyDictionary<string, string>>(
                (a, b) => EfComparers.ReadOnlyDictEqual(a, b),
                v => EfComparers.ReadOnlyDictHash(v),
                v => EfComparers.ReadOnlyDictSnapshot(v));

            entity.Property(e => e.Variables).HasConversion(
                v => JsonHelper.SerializeObject(v),
                v => JsonHelper.DeserializeObject<Dictionary<string, string>>(v)
                         ?? new Dictionary<string, string>())
                .Metadata.SetValueComparer(tokenVarsComparer);

            var parentTokenIdsComparer = new ValueComparer<List<Guid>>(
                (a, b) => EfComparers.ParentTokenIdsEqual(a, b),
                v => EfComparers.ParentTokenIdsHash(v),
                v => EfComparers.ParentTokenIdsSnapshot(v));

            entity.Property<List<Guid>>("_parentTokenIds")
                .HasConversion(
                    v => JsonHelper.SerializeObject(v),
                    v => JsonHelper.DeserializeObject<List<Guid>>(v) ?? new List<Guid>())
                .Metadata.SetValueComparer(parentTokenIdsComparer);

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

        // Configure ExecutedNode
        modelBuilder.Entity<ExecutedNode>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProcessId).IsRequired();
            entity.Property(e => e.NodeId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.NodeName).HasMaxLength(1000);
            entity.Property(e => e.NodeType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ExecutedAt).IsRequired();
            entity.Property(e => e.SequenceOrder).IsRequired();
            entity.Property(e => e.PreviousNodeId).HasMaxLength(500);
            entity.Property(e => e.TokenId).IsRequired();
            entity.Property(e => e.ScopeId);
            entity.Property(e => e.IsCompleted).IsRequired();
            entity.Property(e => e.ArrivedViaFlowId).HasMaxLength(500);
            entity.Property(e => e.ActivityInstanceId);

            // Relationships
            entity.HasOne(e => e.Process)
                .WithMany(p => p.ExecutionNodes)
                .HasForeignKey(e => e.ProcessId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for performance
            entity.HasIndex(e => e.ProcessId);
            entity.HasIndex(e => new { e.ProcessId, e.SequenceOrder });
            entity.HasIndex(e => new { e.ProcessId, e.NodeId         });

      

  // Worker (AggregateRoot) - EF Core configuration (production-ready)
modelBuilder.Entity<Worker>(entity =>
{
    entity.ToTable("Workers");

    entity.HasKey(x => x.Id);

    // ---- Concurrency (strongly recommended for outbox/event-driven engines) ----
    entity.Property<byte[]>("RowVersion")
        .IsRowVersion()
        .IsConcurrencyToken();

    // ---- Required correlation ----
    entity.Property(x => x.ProcessId).IsRequired();
    entity.Property(x => x.TokenId).IsRequired();

    entity.Property(x => x.ElementId)
        .IsRequired()
        .HasMaxLength(256);

    entity.Property(x => x.TaskName)
        .IsRequired()
        .HasMaxLength(512);

    // ---- Enums as strings (stable for dashboards; consider int if you prefer compact) ----
    entity.Property(x => x.Type)
        .IsRequired()
        .HasMaxLength(32)
        .HasConversion<string>();

    entity.Property(x => x.Status)
        .IsRequired()
        .HasMaxLength(32)
        .HasConversion<string>();

    // ---- Timeline ----
    entity.Property(x => x.CreatedAtUtc)
        .IsRequired();

    entity.Property(x => x.ClaimedAtUtc);
    entity.Property(x => x.StartedAtUtc);
    entity.Property(x => x.CompletedAtUtc);

    // ---- Actor / error ----
    entity.Property(x => x.ActorId)
        .HasMaxLength(256);

    entity.Property(x => x.ErrorMessage)
        .HasMaxLength(2000);

    // ---- Dictionaries (JSON) ----
    // IMPORTANT: set to nvarchar(max) for SQL Server; for PostgreSQL prefer jsonb (HasColumnType("jsonb"))
    entity.Property(x => x.Metadata)
        .IsRequired()
        .HasColumnType("text")
        .HasConversion(
            v => JsonHelper.SerializeObject(v ?? new Dictionary<string, string>(StringComparer.Ordinal)),
            v => JsonHelper.DeserializeObject<Dictionary<string, string>>(v)
                 ?? new Dictionary<string, string>(StringComparer.Ordinal));

    entity.Property(x => x.Variables)
        .IsRequired()
        .HasColumnType("text")
        .HasConversion(
            v => JsonHelper.SerializeObject(v ?? new Dictionary<string, string>(StringComparer.Ordinal)),
            v => JsonHelper.DeserializeObject<Dictionary<string, string>>(v)
                 ?? new Dictionary<string, string>(StringComparer.Ordinal));

    // ---- Relationships ----
    // Usually: delete workers when process deleted => Cascade (OK).
    entity.HasOne<Process>()
        .WithMany()
        .HasForeignKey(x => x.ProcessId)
        .OnDelete(DeleteBehavior.Cascade);

    // IMPORTANT:
    // If Token can be deleted/removed at runtime (EndEvent removes token),
    // DO NOT cascade-delete Worker by TokenId. Keep Worker for audit.
    // Use Restrict/NoAction and keep TokenId as correlation (not ownership).
    entity.HasOne<Token>()
        .WithMany()
        .HasForeignKey(x => x.TokenId)
        .OnDelete(DeleteBehavior.Restrict);

    // ---- Indexes ----
    entity.HasIndex(x => x.ProcessId);
    entity.HasIndex(x => x.TokenId); // keep NOT unique unless you're 100% sure: 1 token => max 1 worker always

    entity.HasIndex(x => new { x.ProcessId, x.Status, x.Type });

    entity.HasIndex(x => x.Status);
    entity.HasIndex(x => x.Type);

    entity.HasIndex(x => x.CreatedAtUtc);
    entity.HasIndex(x => x.CompletedAtUtc);

    // Actor/task queries (useful for inbox dashboards)
    entity.HasIndex(x => x.ActorId);

    // Optional: queries by element
    entity.HasIndex(x => new { x.ProcessId, x.ElementId });

    // ---- Ignore domain events list (if BaseAggregateRoot exposes it) ----
    // (Adjust property name to your BaseAggregateRoot implementation)
    entity.Ignore("DomainEvents");
});


        // Configure OutboxMessage
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).IsRequired();
            entity.Property(e => e.OccurredOnUtc).IsRequired();
            entity.Property(e => e.MessageName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.MessageType).HasMaxLength(400);
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.Status).IsRequired().HasConversion<byte>();
            entity.Property(e => e.Attempts).IsRequired();
            entity.Property(e => e.NextAttemptOnUtc);
            entity.Property(e => e.ProcessedOnUtc);
            entity.Property(e => e.LockId);
            entity.Property(e => e.LockedUntilUtc);
            entity.Property(e => e.LastError);
            entity.Property(e => e.CorrelationId);
            entity.Property(e => e.PartitionKey).HasMaxLength(100);
            entity.Property(e => e.AggregateId);

            // Main polling index for efficient message claiming - ordered by oldest messages first
            entity.HasIndex(e => new { e.Status, e.OccurredOnUtc, e.NextAttemptOnUtc })
                .HasFilter("[Status] IN (0, 3)") // Only Pending and Failed
                .HasDatabaseName("IX_OutboxMessages_Status_Occurred_NextAttempt");

            // Lock expiry / recovery index
            entity.HasIndex(e => new { e.Status, e.LockedUntilUtc })
                .HasFilter("[Status] = 1 AND [LockedUntilUtc] IS NOT NULL") // Only Processing with locks
                .HasDatabaseName("IX_OutboxMessages_Status_LockedUntil");

            // Optional per-process ordering/partition index
            entity.HasIndex(e => new { e.PartitionKey, e.Status, e.OccurredOnUtc })
                .HasFilter("[PartitionKey] IS NOT NULL")
                .HasDatabaseName("IX_OutboxMessages_PartitionKey_Status_Occurred");

            // Additional indexes for common queries
            entity.HasIndex(e => e.CorrelationId)
                .HasFilter("[CorrelationId] IS NOT NULL")
                .HasDatabaseName("IX_OutboxMessages_CorrelationId");

            entity.HasIndex(e => e.AggregateId)
                .HasFilter("[AggregateId] IS NOT NULL")
                .HasDatabaseName("IX_OutboxMessages_AggregateId");
        });

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

    public static bool VarsEqual(Dictionary<string, string>? a, Dictionary<string, string>? b)
        => JsonHelper.SerializeObject(a)
           == JsonHelper.SerializeObject(b);

    public static int VarsHash(Dictionary<string, string>? v)
        => JsonHelper.SerializeObject(v).GetHashCode();

    public static Dictionary<string, string> VarsSnapshot(Dictionary<string, string>? v)
        => v is null ? new Dictionary<string, string>() : new Dictionary<string, string>(v);

    // Comparers for IReadOnlyDictionary<string, string> (used by Token.Variables)
    public static bool ReadOnlyDictEqual(IReadOnlyDictionary<string, string>? a, IReadOnlyDictionary<string, string>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;

        return JsonHelper.SerializeObject(a)
               == JsonHelper.SerializeObject(b);
    }

    public static int ReadOnlyDictHash(IReadOnlyDictionary<string, string>? v)
        => v == null ? 0 : JsonHelper.SerializeObject(v).GetHashCode();

    public static IReadOnlyDictionary<string, string> ReadOnlyDictSnapshot(IReadOnlyDictionary<string, string>? v)
    {
        if (v == null) return new Dictionary<string, string>();
        
        // Convert to Dictionary for snapshot (EF Core needs mutable snapshot)
        return new Dictionary<string, string>(v);
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