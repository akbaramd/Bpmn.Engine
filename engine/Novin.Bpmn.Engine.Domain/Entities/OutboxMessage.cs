using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Entities;

/// <summary>
/// Represents a message in the outbox for reliable event publishing
/// </summary>
public class OutboxMessage : BaseEntity
{
    /// <summary>
    /// Unique identifier for the message (also serves as message ID)
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// When the event occurred (used for ordering)
    /// </summary>
    public DateTime OccurredOnUtc { get; private set; }

    /// <summary>
    /// Stable name for the message type (not CLR FullName)
    /// </summary>
    public string MessageName { get; private set; }

    /// <summary>
    /// CLR type of the message (optional, can use registry by name only)
    /// </summary>
    public string? MessageType { get; private set; }

    /// <summary>
    /// Serialized event payload
    /// </summary>
    public string Payload { get; private set; }

    /// <summary>
    /// Current processing status
    /// </summary>
    public OutboxMessageStatus Status { get; private set; }

    /// <summary>
    /// Number of processing attempts
    /// </summary>
    public int Attempts { get; private set; }

    /// <summary>
    /// When the next processing attempt should occur (for backoff scheduling)
    /// </summary>
    public DateTime? NextAttemptOnUtc { get; private set; }

    /// <summary>
    /// When the message was successfully processed
    /// </summary>
    public DateTime? ProcessedOnUtc { get; private set; }

    /// <summary>
    /// Which worker claimed this message
    /// </summary>
    public Guid? LockId { get; private set; }

    /// <summary>
    /// Lease expiration time for the lock
    /// </summary>
    public DateTime? LockedUntilUtc { get; private set; }

    /// <summary>
    /// Last error message from processing attempt
    /// </summary>
    public string? LastError { get; private set; }

    // Performance routing columns

    /// <summary>
    /// Correlation identifier (e.g., ProcessId for grouping related messages)
    /// </summary>
    public Guid? CorrelationId { get; private set; }

    /// <summary>
    /// Partition key for ordering and worker sharding
    /// </summary>
    public string? PartitionKey { get; private set; }

    /// <summary>
    /// Aggregate identifier for debugging and filtering
    /// </summary>
    public Guid? AggregateId { get; private set; }

    private OutboxMessage() { } // EF Core constructor

    public OutboxMessage(
        Guid id,
        DateTime occurredOnUtc,
        string messageName,
        string? messageType,
        string payload,
        Guid? correlationId = null,
        string? partitionKey = null,
        Guid? aggregateId = null)
    {
        Id = id;
        OccurredOnUtc = occurredOnUtc;
        MessageName = messageName ?? throw new ArgumentNullException(nameof(messageName));
        MessageType = messageType;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        Status = OutboxMessageStatus.Pending;
        Attempts = 0;
        CorrelationId = correlationId;
        PartitionKey = partitionKey;
        AggregateId = aggregateId;
    }

    /// <summary>
    /// Mark the message as being processed by a worker
    /// </summary>
    public void MarkAsProcessing(Guid lockId, DateTime lockedUntilUtc)
    {
        Status = OutboxMessageStatus.Processing;
        LockId = lockId;
        LockedUntilUtc = lockedUntilUtc;
        Attempts++;
        NextAttemptOnUtc = null; // Clear any scheduled retry
    }

    /// <summary>
    /// Mark the message as successfully processed
    /// </summary>
    public void MarkAsProcessed(DateTime processedOnUtc)
    {
        Status = OutboxMessageStatus.Processed;
        ProcessedOnUtc = processedOnUtc;
        LastError = null;
    }

    /// <summary>
    /// Mark the message as failed and schedule retry
    /// </summary>
    public void MarkAsFailed(string error, DateTime? nextAttemptOnUtc = null)
    {
        Status = OutboxMessageStatus.Failed;
        LastError = error;
        NextAttemptOnUtc = nextAttemptOnUtc;
        LockId = null;
        LockedUntilUtc = null;
    }

    /// <summary>
    /// Reset the message to pending state for retry
    /// </summary>
    public void ResetToPending()
    {
        Status = OutboxMessageStatus.Pending;
        LockId = null;
        LockedUntilUtc = null;
        LastError = null;
    }

    /// <summary>
    /// Check if the lock has expired
    /// </summary>
    public bool IsLockExpired(DateTime currentTime) =>
        LockedUntilUtc.HasValue && LockedUntilUtc.Value < currentTime;

    public void MarkAsDispatched(DateTime utcNow)
    {
         Status = OutboxMessageStatus.Dispatched;
    }
}