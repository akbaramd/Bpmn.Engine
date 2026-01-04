using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;

public interface IOutboxQueue
{
    ValueTask EnqueueAsync(OutboxQueueItem item, CancellationToken ct = default);

    ValueTask<IReadOnlyList<OutboxQueueEnvelope>> ReadBatchAsync(
        int partition,
        int maxCount,
        TimeSpan block,
        string consumerName,
        CancellationToken ct = default);

    ValueTask AckAsync(int partition, IReadOnlyList<OutboxQueueEnvelope> envelopes, CancellationToken ct = default);

    ValueTask ClaimStuckPendingAsync(
        int partition,
        string consumerName,
        TimeSpan minIdleTime,
        int maxCount,
        CancellationToken ct = default);
}

public sealed record OutboxQueueItem(
    Guid OutboxId,
    string PartitionKey,
    string MessageType,
    string Payload,
    string MessageName,
    DateTime OccurredAtUtc,
    int Attempts);

public sealed record OutboxQueueEnvelope(
    int Partition,
    string StreamId,
    OutboxQueueItem Item);
