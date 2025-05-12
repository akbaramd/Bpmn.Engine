using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Contracts
{
    /// <summary>
    /// An envelope that carries a serialized BPMN event plus metadata
    /// for indexing, querying and type‐safe deserialization.
    /// </summary>
    public record SerializedEvent
    {
        /// <summary>
        /// Unique identifier for this stored event (e.g. GUID or sequence).
        /// </summary>
        public required string Id { get; init; }

        /// <summary>
        /// The process instance this event belongs to (for filtering).
        /// </summary>
        public required string ProcessInstanceId { get; init; }

        /// <summary>
        /// The CLR type name of the event (without namespace).
        /// </summary>
        public required string TypeName { get; init; }

        /// <summary>
        /// The CLR namespace of the event type.
        /// </summary>
        public required string Namespace { get; init; }

        /// <summary>
        /// Fully‐qualified CLR name: Namespace + "." + TypeName.
        /// </summary>
        public string FullName => $"{Namespace}.{TypeName}";

        /// <summary>
        /// The JSON payload of the event.
        /// </summary>
        public required string Payload { get; init; }

        /// <summary>
        /// UTC timestamp when the event was created.
        /// </summary>
        public required DateTime Timestamp { get; init; }
    }

    /// <summary>
    /// Responsible for converting between in‐memory IBpmnEvent instances
    /// and <see cref="SerializedEvent"/> envelopes.
    /// </summary>
    public interface IEventSerializer
    {
        /// <summary>
        /// Convert an IBpmnEvent into a <see cref="SerializedEvent"/> (including JSON payload).
        /// </summary>
        SerializedEvent Serialize(IBpmnEvent @event);

        /// <summary>
        /// Deserialize a <see cref="SerializedEvent"/> back into a CLR IBpmnEvent.
        /// </summary>
        IBpmnEvent Deserialize(SerializedEvent stored);
    }

    /// <summary>
    /// Core persistence API for appending, reading and querying serialized events.
    /// </summary>
    public interface IEventStore
    {
        /// <summary>
        /// Append a single pre‐serialized event to the store.
        /// </summary>
        /// <returns>Position (or sequence number) of the appended event.</returns>
        Task<long> AppendEventAsync(IBpmnEvent @event, CancellationToken cancellationToken = default);

        /// <summary>
        /// Append multiple pre‐serialized events atomically.
        /// </summary>
        /// <returns>Position of the last appended event.</returns>
        Task<long> AppendEventsAsync(IEnumerable<IBpmnEvent> events, CancellationToken cancellationToken = default);

        /// <summary>
        /// Read raw serialized events from a given position.
        /// </summary>
        Task<IReadOnlyList<IBpmnEvent>> ReadEventsAsync(
            long position = 0,
            int count = 100,
            Func<IBpmnEvent, bool>? predicate = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Read events within a specific time range.
        /// </summary>
        Task<IReadOnlyList<IBpmnEvent>> ReadEventsTimeRangeAsync(
            DateTime fromTimestamp,
            DateTime? toTimestamp = null,
            Func<IBpmnEvent, bool>? filter = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Read raw serialized events for a particular process instance.
        /// </summary>
        Task<IReadOnlyList<IBpmnEvent>> ReadProcessInstanceEventsAsync(
            string processInstanceId,
            long position = 0,
            int count = 100,
            Func<IBpmnEvent, bool>? predicate = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribe to new events as they arrive, delivering serialized envelopes.
        /// </summary>
        /// <returns>A subscription ID for later unsubscription.</returns>
        Task<string> SubscribeToEventsAsync(
            Func<IBpmnEvent, Task> handler,
            Func<IBpmnEvent, bool>? predicate = null,
            long position = 0,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancel an existing subscription.
        /// </summary>
        Task UnsubscribeAsync(string subscriptionId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Query service for looking up persisted events by metadata fields.
    /// </summary>
    public interface IEventQueryService
    {
        /// <summary>
        /// Find all events of the given CLR type name.
        /// </summary>
        Task<IReadOnlyList<IBpmnEvent>> QueryByTypeNameAsync(
            string typeName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Find all events within the given CLR namespace.
        /// </summary>
        Task<IReadOnlyList<IBpmnEvent>> QueryByNamespaceAsync(
            string @namespace,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Find all events for a given process instance ID.
        /// </summary>
        Task<IReadOnlyList<IBpmnEvent>> QueryByProcessInstanceIdAsync(
            string processInstanceId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Flexible predicate‐based query over all metadata and payload.
        /// </summary>
        Task<IReadOnlyList<IBpmnEvent>> QueryAsync(
            Func<IBpmnEvent, bool> predicate,
            CancellationToken cancellationToken = default);
    }
}
