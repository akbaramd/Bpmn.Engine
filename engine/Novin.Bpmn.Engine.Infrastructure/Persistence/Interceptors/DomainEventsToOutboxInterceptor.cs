using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor that automatically converts domain events to outbox messages.
/// This ensures outbox writing is impossible to forget.
/// </summary>
public class DomainEventsToOutboxInterceptor : SaveChangesInterceptor
{
    private readonly ILogger<DomainEventsToOutboxInterceptor> _logger;
    private readonly IJsonSerializer _jsonSerializer;

    public DomainEventsToOutboxInterceptor(
        ILogger<DomainEventsToOutboxInterceptor> logger,
        IJsonSerializer jsonSerializer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
    }

    /// <summary>
    /// Intercepts SaveChanges and converts domain events to outbox messages before persisting
    /// </summary>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ConvertDomainEventsToOutboxMessages(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <summary>
    /// Intercepts SaveChangesAsync and converts domain events to outbox messages before persisting
    /// </summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ConvertDomainEventsToOutboxMessages(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Extracts domain events from tracked aggregates and converts them to outbox messages
    /// </summary>
    private void ConvertDomainEventsToOutboxMessages(DbContext? context)
    {
        if (context == null) return;

        // Find all aggregates with domain events
        var aggregatesWithEvents = context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        if (!aggregatesWithEvents.Any()) return;

        _logger.LogDebug("Converting {Count} domain events from {AggregateCount} aggregates to outbox messages",
            aggregatesWithEvents.Sum(a => a.DomainEvents.Count), aggregatesWithEvents.Count);

        var outboxMessages = new List<OutboxMessage>();

        foreach (var aggregate in aggregatesWithEvents)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                var outboxMessage = CreateOutboxMessageFromDomainEvent(domainEvent, aggregate);
                outboxMessages.Add(outboxMessage);

                _logger.LogTrace("Created outbox message {MessageId} for event {EventType}",
                    outboxMessage.Id, domainEvent.GetType().Name);
            }

            // Clear domain events after converting to outbox
            aggregate.ClearDomainEvents();
        }

        // Add outbox messages to context for persistence
        if (context is BpmnEngineDbContext bpmnContext)
        {
            foreach (var outboxMessage in outboxMessages)
            {
                bpmnContext.OutboxMessages.Add(outboxMessage);
            }
        }

        _logger.LogDebug("Added {Count} outbox messages to context", outboxMessages.Count);
    }

    /// <summary>
    /// Creates an outbox message from a domain event
    /// </summary>
    private OutboxMessage CreateOutboxMessageFromDomainEvent(IDomainEvent domainEvent, IAggregateRoot aggregate)
    {
        var messageId = Guid.NewGuid();
        var occurredOnUtc = DateTime.UtcNow;

        // Serialize the domain event
        var payload = _jsonSerializer.SerializeObject(domainEvent);

        // Use stable message name (not CLR FullName)
        var messageName = GetStableMessageName(domainEvent);

        // Extract correlation/aggregate IDs from the event if available
        var correlationId = ExtractCorrelationId(domainEvent);
        var aggregateId = ExtractAggregateId(domainEvent, aggregate);

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
    /// Extracts aggregate ID from the aggregate or event
    /// </summary>
    private static Guid? ExtractAggregateId(IDomainEvent domainEvent, IAggregateRoot aggregate)
    {
        // Try to get aggregate ID from the event first
        var aggregateIdProperty = domainEvent.GetType().GetProperty("AggregateId") ??
                                 domainEvent.GetType().GetProperty("Id");

        if (aggregateIdProperty != null && aggregateIdProperty.PropertyType == typeof(Guid))
        {
            return (Guid?)aggregateIdProperty.GetValue(domainEvent);
        }

        // Fall back to aggregate ID if available
        if (aggregate is BaseEntity baseEntity)
        {
            return baseEntity.Id;
        }

        return null;
    }
}