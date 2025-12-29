using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;

/// <summary>
/// Interface for claiming batches of outbox messages for processing.
/// This ensures safe concurrent access across multiple worker nodes.
/// </summary>
public interface IOutboxBatchClaimer
{
    /// <summary>
    /// Claims a batch of pending outbox messages for processing.
    /// </summary>
    /// <param name="batchSize">Maximum number of messages to claim</param>
    /// <param name="lease">Time span for which the messages should be locked</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Collection of claimed outbox messages</returns>
    Task<IReadOnlyList<OutboxMessage>> ClaimAsync(int batchSize, TimeSpan lease, CancellationToken ct);
}