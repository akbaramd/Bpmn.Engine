namespace Novin.Bpmn.Engine.Infrastructure.BackgroundServices;

public sealed record OutboxEventEnvelope(
    Guid OutboxId,
    string PartitionKey,
    string MessageName,
    string MessageType,
    string Payload,
    DateTime OccurredAtUtc,
    int Attempts,
    IReadOnlyDictionary<string, string>? Headers = null
);