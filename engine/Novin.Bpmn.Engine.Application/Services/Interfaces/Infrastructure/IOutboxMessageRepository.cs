using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;

/// <summary>
/// Repository interface for outbox message operations.
/// </summary>
public interface IOutboxMessageRepository
{
    /// <summary>
    /// Adds a new outbox message to the repository.
    /// </summary>
    /// <param name="message">The outbox message to add</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the operation</returns>
    Task AddAsync(OutboxMessage message, CancellationToken ct);

    /// <summary>
    /// Updates an existing outbox message.
    /// </summary>
    /// <param name="message">The outbox message to update</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the operation</returns>
    Task UpdateAsync(OutboxMessage message, CancellationToken ct);

    /// <summary>
    /// Gets outbox messages that have expired locks for recovery.
    /// </summary>
    /// <param name="currentTime">Current UTC time</param>
    /// <param name="batchSize">Maximum number of messages to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Collection of messages with expired locks</returns>
    Task<IReadOnlyList<OutboxMessage>> GetExpiredLocksAsync(DateTime currentTime, int batchSize, CancellationToken ct);

    /// <summary>
    /// Gets outbox messages by their IDs.
    /// </summary>
    /// <param name="ids">Collection of message IDs</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Collection of outbox messages</returns>
    Task<IReadOnlyList<OutboxMessage>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>
    /// Gets pending messages that are ready for retry.
    /// </summary>
    /// <param name="currentTime">Current UTC time</param>
    /// <param name="batchSize">Maximum number of messages to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Collection of messages ready for retry</returns>
    Task<IReadOnlyList<OutboxMessage>> GetPendingForRetryAsync(DateTime currentTime, int batchSize, CancellationToken ct);
    Task<OutboxMessage> GetByIdAsync(Guid outboxId, CancellationToken ct);
    Task MarkProcessedAsync(Guid outboxId, DateTime utcNow, CancellationToken ct);
    Task MarkFailedAsync(Guid outboxId, string message, CancellationToken ct);
    Task MarkDispatchedAsync(List<Guid> ids, DateTime utcNow, CancellationToken ct);
}