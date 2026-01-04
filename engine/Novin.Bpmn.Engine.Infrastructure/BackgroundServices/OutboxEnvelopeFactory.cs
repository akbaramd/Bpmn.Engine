using System;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Infrastructure.BackgroundServices;

namespace Novin.Bpmn.Engine.Infrastructure.Outbox.MassTransit;

public static class OutboxEnvelopeFactory
{
    public static OutboxEventEnvelope FromDomainEvent(
        Guid outboxId,
        IDomainEvent e,
        string? partitionKey,
        IJsonSerializer json,
        DateTime occurredAtUtc,
        int attempts = 0)
    {
        if (e is null) throw new ArgumentNullException(nameof(e));
        if (json is null) throw new ArgumentNullException(nameof(json));

        return new OutboxEventEnvelope(
            OutboxId: outboxId,
            PartitionKey: string.IsNullOrWhiteSpace(partitionKey) ? "global" : partitionKey!,
            MessageName: StableMessageName(e),
            MessageType: e.GetType().AssemblyQualifiedName ?? e.GetType().FullName ?? e.GetType().Name,
            Payload: json.SerializeObject(e),
            OccurredAtUtc: occurredAtUtc,
            Attempts: attempts
        );
    }

    private static string StableMessageName(IDomainEvent e)
    {
        var n = e.GetType().Name;

        if (n.EndsWith("Event", StringComparison.OrdinalIgnoreCase))
            n = n[..^5];
        else if (n.EndsWith("DomainEvent", StringComparison.OrdinalIgnoreCase))
            n = n[..^11];

        return n;
    }
}