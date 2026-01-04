using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.Repositories;
using Novin.Bpmn.Engine.Infrastructure.Outbox.MassTransit; // IOutboxEventPublisher + OutboxEnvelopeFactory
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Novin.Bpmn.Engine.Infrastructure.BackgroundServices;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.UnitOfWork;

/// <summary>
/// EF UnitOfWork with:
/// - DB transaction (execution strategy compatible)
/// - DomainEvents collected from aggregates
/// - Post-commit publish to queue via IOutboxEventPublisher (BEST-EFFORT)
///
/// IMPORTANT: This is NOT a durable outbox. If the process crashes after DB commit and before publish,
/// events may be missed unless you have a rebuild strategy or durable outbox.
/// </summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly BpmnEngineDbContext _context;
    private readonly ILogger<EfUnitOfWork> _logger;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly IOutboxEventPublisher _publisher;

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
        IOutboxEventPublisher publisher)
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
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        // Nested tx: run only; outer transaction owner will Save/Commit/Publish
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

            // Post-commit publish (best-effort, do not throw)
            await PublishDomainEventsAsync(committedEvents, ct);
        });
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null) return;
        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        // No explicit tx: SaveChanges + publish
        if (_currentTransaction == null)
        {
            var committedEvents = await CollectDomainEventsAndSaveAsync(cancellationToken);
            await PublishDomainEventsAsync(committedEvents, cancellationToken);
            return;
        }

        // Explicit tx: SaveChanges + commit + publish
        var events = await CollectDomainEventsAndSaveAsync(cancellationToken);
        await _currentTransaction.CommitAsync(cancellationToken);

        await _currentTransaction.DisposeAsync();
        _currentTransaction = null;

        await PublishDomainEventsAsync(events, cancellationToken);
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
    /// Post-commit: publish DomainEvents to partitioned queue.
    /// Best-effort: do not throw (DB is already committed).
    /// </summary>
    private async Task PublishDomainEventsAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct)
    {
        if (events == null || events.Count == 0) return;

        try
        {
            // برای حفظ order اگر لازم داری: این foreach را sequential نگه دار.
            // برای throughput بیشتر: می‌تونی محدودیت concurrency بذاری (مثلاً 8).
            foreach (var e in events)
            {
                ct.ThrowIfCancellationRequested();

                var occurredAtUtc = ExtractOccurredAtUtcOrNow(e);
                var correlationId = ExtractCorrelationId(e);
                var partitionKey = correlationId?.ToString("N") ?? "global";

                // ✅ خیلی مهم: OutboxId باید deterministic باشد تا retryها idempotent شوند.
                var outboxId = GetDeterministicOutboxId(e, correlationId);

                var env = OutboxEnvelopeFactory.FromDomainEvent(
                    outboxId: outboxId,
                    e: e,
                    partitionKey: partitionKey,
                    json: _jsonSerializer,
                    occurredAtUtc: occurredAtUtc,
                    attempts: 0
                );

                await _publisher.PublishAsync(env, ct).ConfigureAwait(false);
            }

            _logger.LogDebug("[BUS] Published {Count} domain events.", events.Count);
        }
        catch (Exception ex)
        {
            // DB already committed: do NOT throw
            _logger.LogWarning(ex, "[BUS] Publish failed (best-effort). Count={Count}", events.Count);
        }
    }

    private static DateTime ExtractOccurredAtUtcOrNow(IDomainEvent e)
    {
        // اگر DomainEvent شما OccurredAtUtc دارد استفاده کن
        var p = e.GetType().GetProperty("OccurredAtUtc", BindingFlags.Instance | BindingFlags.Public);
        if (p != null && p.PropertyType == typeof(DateTime))
        {
            var v = (DateTime)p.GetValue(e)!;
            if (v != default) return v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime();
        }
        return DateTime.UtcNow;
    }

    /// <summary>
    /// Deterministic OutboxId:
    /// - Prefer EventId (Guid) if exists
    /// - Else hash(type + correlation + aggregate) => Guid (stable)
    /// </summary>
    private static Guid GetDeterministicOutboxId(IDomainEvent e, Guid? correlationId)
    {
        var eventId = TryGetGuidProperty(e, "EventId")
                   ?? TryGetGuidProperty(e, "Id"); // اگر EventId نداری ولی Id داری

        if (eventId.HasValue)
            return eventId.Value;

        var agg = ExtractAggregateId(e);

        // Stable key (same event will produce same id) — البته اگر event واقعاً unique نباشه، collision منطقی ممکنه.
        var raw = $"{e.GetType().AssemblyQualifiedName}|corr:{correlationId?.ToString("N")}|agg:{agg?.ToString("N")}";
        return DeterministicGuid(raw);
    }

    private static Guid DeterministicGuid(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        // take first 16 bytes for Guid
        Span<byte> g = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(g);
        return new Guid(g);
    }

    private static Guid? TryGetGuidProperty(object obj, string name)
    {
        var p = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (p == null || p.PropertyType != typeof(Guid)) return null;
        return (Guid?)p.GetValue(obj);
    }

    private static Guid? ExtractCorrelationId(IDomainEvent domainEvent)
    {
        var correlationProperties = new[] { "ProcessId", "CorrelationId" };

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
