using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Repositories;
using Novin.Bpmn.Engine.Infrastructure.Outbox.Elastices;
using System.Reflection;
using System.Text.Json.Nodes;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.UnitOfWork;

/// <summary>
/// EF UnitOfWork with:
/// - DB transaction (execution strategy compatible)
/// - DomainEvents collected from aggregates
/// - Post-commit projection to Elasticsearch (BEST-EFFORT)
///
/// IMPORTANT: This is NOT a durable outbox. If the process crashes after DB commit and before ES write,
/// ES projection may miss events unless you have a rebuild/reindex strategy.
/// </summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly BpmnEngineDbContext _context;
    private readonly ILogger<EfUnitOfWork> _logger;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly IElasticOutboxWriter _elasticWriter;

    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    public IDeploymentRepository Deployments { get; }
    public IUserTaskInstanceRepository UserTaskInstances { get; }
    public IProcessRepository Processes { get; }
    public ITokenRepository Tokens { get; }
    public IWorkerRepository Workers { get; }
    public IIncidentRepository Incidents { get; }
    public IBoundarySubscriptionRepository BoundarySubscriptions { get; }
    public INodeInstanceRepository NodeInstances { get; }

    public bool IsInTransaction => _currentTransaction != null;

    public EfUnitOfWork(
        BpmnEngineDbContext context,
        IDeploymentRepository deploymentRepository,
        IProcessRepository processRepository,
        ITokenRepository tokenRepository,
        IIncidentRepository incidentRepository,
        IWorkerRepository workerRepository,
        IBoundarySubscriptionRepository boundarySubscriptionRepository,
        INodeInstanceRepository nodeRepository,
        IUserTaskInstanceRepository userTaskInstances,
        ILogger<EfUnitOfWork> logger,
        IJsonSerializer jsonSerializer,
        IElasticOutboxWriter elasticWriter)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        Deployments = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
        Processes = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
        Tokens = tokenRepository ?? throw new ArgumentNullException(nameof(tokenRepository));
        Incidents = incidentRepository ?? throw new ArgumentNullException(nameof(incidentRepository));
        BoundarySubscriptions = boundarySubscriptionRepository ?? throw new ArgumentNullException(nameof(boundarySubscriptionRepository));
        Workers = workerRepository ?? throw new ArgumentNullException(nameof(workerRepository));
        NodeInstances = nodeRepository ?? throw new ArgumentNullException(nameof(nodeRepository));
        UserTaskInstances = userTaskInstances ?? throw new ArgumentNullException(nameof(userTaskInstances));

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
        _elasticWriter = elasticWriter ?? throw new ArgumentNullException(nameof(elasticWriter));
    }

    /// <summary>
    /// Executes user code inside a retryable execution strategy transaction (Npgsql retry strategy compatible).
    /// Commits DB first, then does ES projection (best-effort, non-throwing).
    /// </summary>
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        // Nested tx: run only; outer transaction owner will Save/Commit/Project
        if (_currentTransaction != null)
        {
            await action(ct);
            return;
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            IReadOnlyList<IDomainEvent> committedEvents = Array.Empty<IDomainEvent>();

            await BeginTransactionAsync(ct);
            try
            {
                await action(ct);

                // SaveChanges + collect domain events in same transaction boundary
                committedEvents = await CollectDomainEventsAndSaveAsync(ct);

                await _currentTransaction!.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExecuteInTransactionAsync failed. Rolling back.");
                try { await _currentTransaction?.RollbackAsync(ct)!; }
                catch (Exception rbEx) { _logger.LogError(rbEx, "Rollback failed."); }
                throw;
            }
            finally
            {
                if (_currentTransaction != null)
                    await _currentTransaction.DisposeAsync();

                _currentTransaction = null;
            }

            // Post-commit projection (best-effort, do not throw)
            await ProjectToElasticAsync(committedEvents, ct);
        });
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null) return;
        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <summary>
    /// If you call this directly (outside ExecuteInTransactionAsync), it will SaveChanges once.
    /// Then ES projection (best-effort).
    /// </summary>
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        // No explicit tx: SaveChanges + projection
        if (_currentTransaction == null)
        {
            var committedEvents = await CollectDomainEventsAndSaveAsync(cancellationToken);
            await ProjectToElasticAsync(committedEvents, cancellationToken);
            return;
        }

        // Explicit tx: SaveChanges + commit + projection
        var events = await CollectDomainEventsAndSaveAsync(cancellationToken);
        await _currentTransaction.CommitAsync(cancellationToken);

        await _currentTransaction.DisposeAsync();
        _currentTransaction = null;

        await ProjectToElasticAsync(events, cancellationToken);
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null) return;

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
            _context.ChangeTracker.Clear();
        }
    }

    /// <summary>
    /// Collect domain events, clear them from aggregates, then SaveChanges.
    /// This method is the single point that turns in-memory DomainEvents into committed facts.
    /// </summary>
    private async Task<IReadOnlyList<IDomainEvent>> CollectDomainEventsAndSaveAsync(CancellationToken ct)
    {
        var aggregatesWithEvents = _context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        if (aggregatesWithEvents.Count == 0)
        {
            await _context.SaveChangesAsync(ct);
            return Array.Empty<IDomainEvent>();
        }

        var events = aggregatesWithEvents
            .SelectMany(a => a.DomainEvents)
            .ToList();

        aggregatesWithEvents.ForEach(a => a.ClearDomainEvents());

        await _context.SaveChangesAsync(ct);
        return events;
    }

    /// <summary>
    /// Post-commit: bulk index DomainEvents into Elasticsearch.
    /// Best-effort: do not throw (DB is already committed).
    /// </summary>
    private async Task ProjectToElasticAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct)
    {
        if (events == null || events.Count == 0) return;

        try
        {
            // NOTE: This assumes your writer supports bulk.
            // If your IElasticOutboxWriter currently only has WritePendingAsync, add a bulk method there.
            var docs = new List<(string Id, OutboxDoc Doc)>(events.Count);

            foreach (var e in events)
            {
                var id = Guid.NewGuid().ToString();
                var doc = ToElasticDoc(e);
                docs.Add((id, doc));
            }

            await _elasticWriter.WritePendingBulkAsync(docs, ct);

            _logger.LogDebug("[ES] Indexed {Count} domain events.", docs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ES] Projection indexing failed (no retry here). Count={Count}", events.Count);
        }
    }

    /// <summary>
    /// Builds the ES document from DomainEvent (no DB outbox row required).
    /// </summary>
    private OutboxDoc ToElasticDoc(IDomainEvent e)
    {
        var occurredAtUtc = DateTime.UtcNow; // If your events carry OccurredAtUtc, prefer that.
        var payloadJson = _jsonSerializer.SerializeObject(e);

        JsonNode? payloadNode;
        try
        {
            payloadNode = string.IsNullOrWhiteSpace(payloadJson) ? null : JsonNode.Parse(payloadJson);
        }
        catch
        {
            payloadNode = new JsonObject { ["raw"] = payloadJson };
        }

        var messageName = GetStableMessageName(e);
        var correlationId = ExtractCorrelationId(e);
        var aggregateId = ExtractAggregateId(e);

        return new OutboxDoc
        {
            Status = "pending",
            OccurredAtUtc = occurredAtUtc,

            Attempts = 0,
            NextAttemptOnUtc = null,
            LockedUntilUtc = null,
            LockId = null,

            MessageName = messageName,
            MessageType = e.GetType().AssemblyQualifiedName,
            PartitionKey = correlationId?.ToString() ?? "global",

            CorrelationId = correlationId,
            AggregateId = aggregateId,

            LastError = null,
            Payload = payloadNode
        };
    }

    /// <summary>
    /// ES-only projection needs idempotency. Best is DomainEvent.EventId (Guid).
    /// Fallback is a stable-ish hash, but not perfect.
    /// </summary>
    private string GetDeterministicEventIdOrFallback(IDomainEvent e)
    {
        // Prefer explicit EventId if present
        var eventId = TryGetGuidProperty(e, "EventId")
                   ?? TryGetGuidProperty(e, "Id"); // if your events use Id as EventId

        if (eventId.HasValue)
            return eventId.Value.ToString("N");

        // Fallback: (type + correlation + aggregate + ticks) -> duplicates possible across retries.
        // We log once per event type to make this visible.
        var corr = ExtractCorrelationId(e);
        var agg = ExtractAggregateId(e);

        var raw = $"{e.GetType().FullName}|{corr?.ToString("N")}|{agg?.ToString("N")}|{DateTime.UtcNow.Ticks}";
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));

        _logger.LogWarning(
            "DomainEvent has no EventId. Using hash-based ES id (may duplicate). Type={Type}",
            e.GetType().FullName);

        return hash;
    }

    private static Guid? TryGetGuidProperty(object obj, string name)
    {
        var p = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (p == null || p.PropertyType != typeof(Guid)) return null;
        return (Guid?)p.GetValue(obj);
    }

    private static string GetStableMessageName(IDomainEvent domainEvent)
    {
        var typeName = domainEvent.GetType().Name;

        if (typeName.EndsWith("Event", StringComparison.OrdinalIgnoreCase))
            typeName = typeName[..^5];
        else if (typeName.EndsWith("DomainEvent", StringComparison.OrdinalIgnoreCase))
            typeName = typeName[..^11];

        return typeName;
    }

    private static Guid? ExtractCorrelationId(IDomainEvent domainEvent)
    {
        var correlationProperties = new[] { "ProcessId", "CorrelationId", "Id" };

        foreach (var propertyName in correlationProperties)
        {
            var property = domainEvent.GetType().GetProperty(propertyName);
            if (property != null && property.PropertyType == typeof(Guid))
                return (Guid?)property.GetValue(domainEvent);
        }

        return null;
    }

    private static Guid? ExtractAggregateId(IDomainEvent domainEvent)
    {
        var aggregateIdProperty =
            domainEvent.GetType().GetProperty("AggregateId") ??
            domainEvent.GetType().GetProperty("Id");

        if (aggregateIdProperty != null && aggregateIdProperty.PropertyType == typeof(Guid))
            return (Guid?)aggregateIdProperty.GetValue(domainEvent);

        return null;
    }

    public void TrackAggregate(IAggregateRoot aggregate)
    {
        if (aggregate == null) throw new ArgumentNullException(nameof(aggregate));

        var entry = _context.Entry(aggregate);
        if (entry.State == EntityState.Detached)
            _context.Attach(aggregate);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _currentTransaction?.Dispose();
        _context.Dispose();
    }
}
