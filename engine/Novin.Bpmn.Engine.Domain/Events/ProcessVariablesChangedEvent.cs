using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

/// <summary>
/// Emitted when process variables are added, updated, or removed in a single patch.
/// </summary>
public sealed class ProcessVariablesChangedEvent : BaseDomainEvent
{
    public Guid ProcessId { get; }
    public IReadOnlyDictionary<string, string> Upserts { get; }
    public IReadOnlyCollection<string> Removals { get; }
    public DateTime OccurredAtUtc { get; }

    public ProcessVariablesChangedEvent(
        Guid processId,
        IReadOnlyDictionary<string, string> upserts,
        IReadOnlyCollection<string> removals,
        DateTime occurredAtUtc)
    {
        ProcessId = processId;
        Upserts = upserts;
        Removals = removals;
        OccurredAtUtc = occurredAtUtc;
    }
}

