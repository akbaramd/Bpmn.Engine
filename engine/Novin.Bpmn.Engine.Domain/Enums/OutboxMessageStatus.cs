namespace Novin.Bpmn.Engine.Domain.Entities;

/// <summary>
/// Status of an outbox message
/// </summary>
public enum OutboxMessageStatus : byte
{
    /// <summary>
    /// Message is waiting to be processed
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Message is currently being processed by a worker
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Message has been successfully processed
    /// </summary>
    Processed = 2,

    /// <summary>
    /// Message processing failed and is scheduled for retry
    /// </summary>
    Failed = 3,
    Dispatched = 4
}