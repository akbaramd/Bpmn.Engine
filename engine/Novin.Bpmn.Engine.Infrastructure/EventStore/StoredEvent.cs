using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Infrastructure.EventStore;

/// <summary>
/// Represents a stored domain event
/// </summary>
public class StoredEvent
{
    public Guid EventId { get; set; }
    public Guid AggregateId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EventData { get; set; } = string.Empty;
    public int AggregateVersion { get; set; }
    public DateTime OccurredOn { get; set; }
}

