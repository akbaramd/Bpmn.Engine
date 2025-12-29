using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Infrastructure.EventBus;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.UnitOfWork;

public class EfUnitOfWork : IUnitOfWork
{
    private readonly BpmnEngineDbContext _context;
    private readonly ILogger<EfUnitOfWork> _logger;
    private readonly IJsonSerializer _jsonSerializer;

    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    public IDeploymentRepository Deployments { get; }
    public IProcessRepository Processes { get; }
    public ITokenRepository Tokens { get; }
    public IWorkerRepository Workers { get; } 
    public IIncidentRepository Incidents { get; }
    public IBoundarySubscriptionRepository BoundarySubscriptions { get; }

    /// <summary>
    /// Checks if a transaction is currently active
    /// </summary>
    public bool IsInTransaction => _currentTransaction != null;

    public EfUnitOfWork(
        BpmnEngineDbContext context,
        IDeploymentRepository deploymentRepository,
        IProcessRepository processRepository,
        ITokenRepository tokenRepository,
        IIncidentRepository incidentRepository,
        IWorkerRepository workerRepository,
        IBoundarySubscriptionRepository boundarySubscriptionRepository,
        ILogger<EfUnitOfWork> logger,
        IJsonSerializer jsonSerializer)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Deployments = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
        Processes = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
        Tokens = tokenRepository ?? throw new ArgumentNullException(nameof(tokenRepository));
        Incidents = incidentRepository ?? throw new ArgumentNullException(nameof(incidentRepository));
        BoundarySubscriptions = boundarySubscriptionRepository ?? throw new ArgumentNullException(nameof(boundarySubscriptionRepository));
        Workers = workerRepository ?? throw new ArgumentNullException(nameof(workerRepository));
        
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
    }

    // ---------------- Transaction API ----------------

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        // Nested / already in tx: فقط action
        if (_currentTransaction != null)
        {
            await action(ct);
            return;
        }

        await BeginTransactionAsync(ct);

        try
        {
            await action(ct);
            await ProcessDomainEventsAndSaveAsync(ct);
            await CommitTransactionAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExecuteInTransactionAsync failed. Rolling back.");
            try
            {
                await RollbackTransactionAsync(ct);
            }
            catch (Exception rbEx)
            {
                _logger.LogError(rbEx, "Rollback failed.");
            }

            throw;
        }
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null) return;

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        // اگر transaction explicit نداریم، فقط save کن
        if (_currentTransaction == null)
        {
            await ProcessDomainEventsAndSaveAsync(cancellationToken);
            return;
        }

        try
        {
            await ProcessDomainEventsAndSaveAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CommitTransactionAsync failed.");
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
            }

            _currentTransaction = null;
        }
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

            // برای جلوگیری از state ناسازگار بعد از rollback
            _context.ChangeTracker.Clear();
        }
    }

    // ---------------- Transaction Processing ----------------

    /// <summary>
    /// Processes domain events and saves changes: adds events to outbox before SaveChanges
    /// </summary>
    private async Task ProcessDomainEventsAndSaveAsync(CancellationToken cancellationToken)
    {
        // Collect domain events from aggregates
        var aggregatesWithEvents = _context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = aggregatesWithEvents
            .SelectMany(x => x.DomainEvents)
            .ToList();

        // Clear events from aggregates
        aggregatesWithEvents.ForEach(e => e.ClearDomainEvents());

        // Convert domain events to outbox messages and add to context BEFORE saving
        if (domainEvents.Any())
        {
            var outboxMessages = ConvertDomainEventsToOutboxMessages(domainEvents);
            foreach (var outboxMessage in outboxMessages)
            {
                await _context.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
            }

            _logger.LogDebug("Added {Count} outbox messages to context before save", outboxMessages.Count);
        }

        // Save both data changes and outbox messages
        await _context.SaveChangesAsync(cancellationToken);

      
    }


    /// <summary>
    /// Converts domain events to outbox messages
    /// </summary>
    private List<OutboxMessage> ConvertDomainEventsToOutboxMessages(List<IDomainEvent> domainEvents)
    {
        var outboxMessages = new List<OutboxMessage>();

        foreach (var domainEvent in domainEvents)
        {
            var outboxMessage = CreateOutboxMessageFromDomainEvent(domainEvent);
            outboxMessages.Add(outboxMessage);

            _logger.LogTrace("Created outbox message {MessageId} for event {EventType}",
                outboxMessage.Id, domainEvent.GetType().Name);
        }

        return outboxMessages;
    }

    /// <summary>
    /// Creates an outbox message from a domain event
    /// </summary>
    private OutboxMessage CreateOutboxMessageFromDomainEvent(IDomainEvent domainEvent)
    {
        var messageId = Guid.NewGuid();
        var occurredOnUtc = DateTime.UtcNow;

        // Serialize the domain event using centralized JSON serializer
        var payload = _jsonSerializer.SerializeObject(domainEvent);

        // Use stable message name (not CLR FullName)
        var messageName = GetStableMessageName(domainEvent);

        // Extract correlation/aggregate IDs from the event if available
        var correlationId = ExtractCorrelationId(domainEvent);
        var aggregateId = ExtractAggregateId(domainEvent);

        return new OutboxMessage(
            id: messageId,
            occurredOnUtc: occurredOnUtc,
            messageName: messageName,
            messageType: domainEvent.GetType().AssemblyQualifiedName,
            payload: payload,
            correlationId: correlationId,
            partitionKey: null, // Could be set based on business logic
            aggregateId: aggregateId
        );
    }

    /// <summary>
    /// Gets a stable message name for the domain event (not CLR type name)
    /// </summary>
    private static string GetStableMessageName(IDomainEvent domainEvent)
    {
        // For now, use the class name without namespace
        // In production, you might want to use attributes or configuration
        var typeName = domainEvent.GetType().Name;

        // Remove common suffixes like "Event", "DomainEvent", etc.
        if (typeName.EndsWith("Event", StringComparison.OrdinalIgnoreCase))
        {
            typeName = typeName.Substring(0, typeName.Length - 5);
        }
        else if (typeName.EndsWith("DomainEvent", StringComparison.OrdinalIgnoreCase))
        {
            typeName = typeName.Substring(0, typeName.Length - 11);
        }

        return typeName;
    }

    /// <summary>
    /// Extracts correlation ID from domain event (e.g., ProcessId)
    /// </summary>
    private static Guid? ExtractCorrelationId(IDomainEvent domainEvent)
    {
        // Try common property names for correlation IDs
        var correlationProperties = new[] { "ProcessId", "CorrelationId", "Id" };

        foreach (var propertyName in correlationProperties)
        {
            var property = domainEvent.GetType().GetProperty(propertyName);
            if (property != null && property.PropertyType == typeof(Guid))
            {
                return (Guid?)property.GetValue(domainEvent);
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts aggregate ID from the event
    /// </summary>
    private static Guid? ExtractAggregateId(IDomainEvent domainEvent)
    {
        // Try to get aggregate ID from the event first
        var aggregateIdProperty = domainEvent.GetType().GetProperty("AggregateId") ??
                                 domainEvent.GetType().GetProperty("Id");

        if (aggregateIdProperty != null && aggregateIdProperty.PropertyType == typeof(Guid))
        {
            return (Guid?)aggregateIdProperty.GetValue(domainEvent);
        }

        return null;
    }

    // ---------------- Tracking ----------------

    public void TrackAggregate(IAggregateRoot aggregate)
    {
        if (aggregate == null) throw new ArgumentNullException(nameof(aggregate));

        var entry = _context.Entry(aggregate);
        if (entry.State == EntityState.Detached)
            _context.Attach(aggregate);
    }

    // ---------------- Dispose ----------------

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _currentTransaction?.Dispose();
        _context.Dispose();
    }
}
