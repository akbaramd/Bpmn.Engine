using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public sealed record ProcessMetadataChangedEvent(
    Guid ProcessId,
    IReadOnlyDictionary<string, string> Upserts,
    IReadOnlyCollection<string> Removals,
    DateTime OccurredAtUtc
) : IDomainEvent;
