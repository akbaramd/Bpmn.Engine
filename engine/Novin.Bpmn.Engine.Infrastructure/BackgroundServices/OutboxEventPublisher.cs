using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Novin.Bpmn.Engine.Infrastructure.BackgroundServices;

namespace Novin.Bpmn.Engine.Infrastructure.Outbox.MassTransit;

public sealed class OutboxEventPublisher : IOutboxEventPublisher
{
    private readonly IBus _bus;
    private readonly int _partitions;

    public OutboxEventPublisher(IBus bus, int partitions)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _partitions = partitions <= 0 ? 16 : partitions;
    }

    public async Task PublishAsync(OutboxEventEnvelope envelope, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var p = OutboxPartitioning.PickPartition(envelope.PartitionKey, _partitions);
        var endpoint = await _bus.GetSendEndpoint(new Uri($"queue:{OutboxQueueNames.PartitionQueue(p)}"));
        await endpoint.Send(envelope, ct);
    }
}