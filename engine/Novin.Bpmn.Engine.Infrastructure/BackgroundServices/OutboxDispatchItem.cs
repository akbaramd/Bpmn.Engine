namespace Novin.Bpmn.Engine.Infrastructure.Outbox.Redis;

public sealed record OutboxDispatchItem(
    Guid OutboxId,
    string PartitionKey,
    string MessageType,
    string Payload,
    string MessageName,
    DateTime OccurredAtUtc,
    int Attempts
);