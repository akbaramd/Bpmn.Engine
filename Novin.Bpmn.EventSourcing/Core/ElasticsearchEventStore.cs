using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Microsoft.Extensions.Logging;
using Nest;
using Novin.Bpmn.EventSourcing.Contracts;

namespace Novin.Bpmn.EventSourcing.Core
{
    /// <summary>
    /// Custom exception for Elasticsearch client errors
    /// </summary>
    public class ElasticsearchClientException : Exception
    {
        public ElasticsearchClientException(string message) : base(message) { }
        public ElasticsearchClientException(string message, Exception inner) : base(message, inner) { }
    }

/// <summary>
    /// JSON‐based serializer for BPMN events.
    /// </summary>
    public class JsonEventSerializer : IEventSerializer
    {
        private readonly JsonSerializerOptions _options;

        public JsonEventSerializer(JsonSerializerOptions? options = null)
        {
            // configure your JSON settings (camel-case, enum as strings, etc.)
            _options = options ?? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters =
                {
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
                },
                WriteIndented = false
            };
        }

        /// <inheritdoc/>
        public SerializedEvent Serialize(IBpmnEvent @event)
        {
            if (@event is null) throw new ArgumentNullException(nameof(@event));

            var eventType = @event.GetType();
            var payload   = JsonSerializer.Serialize(@event, eventType, _options);

            return new SerializedEvent
            {
                Id                = Guid.NewGuid().ToString(),
                ProcessInstanceId = @event.InstanceId,
                TypeName          = eventType.Name,
                Namespace         = eventType.Namespace ?? string.Empty,
                Payload           = payload,
                Timestamp         = @event.Timestamp
            };
        }

        /// <inheritdoc/>
        public IBpmnEvent Deserialize(SerializedEvent stored)
        {
            if (stored is null) throw new ArgumentNullException(nameof(stored));

            // locate the CLR type by its full name
            var fullName = stored.FullName;
            var type     = Type.GetType(fullName)
                           ?? throw new InvalidOperationException($"Type '{fullName}' not found.");

            var @event = JsonSerializer.Deserialize(stored.Payload, type, _options)
                        as IBpmnEvent
                    ?? throw new InvalidOperationException($"Payload did not deserialize to IBpmnEvent.");

            return @event;
        }
    }
    /// <summary>
    /// Elasticsearch‐backed implementation of IEventStore.
    /// </summary>
    public class ElasticsearchEventStore : IEventStore, IEventQueryService
    {
        private const string IndexName = "bpmn-events";
        private readonly IElasticClient _client;
        private readonly ILogger<ElasticsearchEventStore> _logger;
        private readonly IEventSerializer _serializer;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _subscriptions
            = new ConcurrentDictionary<string, CancellationTokenSource>();

        // Position tracking
        private long _lastPosition = 0;

        public ElasticsearchEventStore(
            IElasticClient client,
            ILogger<ElasticsearchEventStore> logger,
            IEventSerializer serializer)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            EnsureIndexExistsAsync().GetAwaiter().GetResult();
        }

        private async Task EnsureIndexExistsAsync()
        {
            var exists = await _client.Indices.ExistsAsync(IndexName);
            if (exists.Exists) return;

            var create = await _client.Indices.CreateAsync(IndexName, c => c
                .Settings(s => s
                    .NumberOfShards(1)
                    .NumberOfReplicas(1)
                    .RefreshInterval("1s"))
                .Map<SerializedEvent>(m => m
                    .AutoMap()
                    .Properties(ps => ps
                        .Keyword(k => k.Name(e => e.ProcessInstanceId).IgnoreAbove(256))
                        .Keyword(k => k.Name(e => e.TypeName).IgnoreAbove(128))
                        .Keyword(k => k.Name(e => e.Namespace).IgnoreAbove(256))
                        .Keyword(k => k.Name(e => e.FullName).IgnoreAbove(512))
                        .Text(t => t.Name(e => e.Payload).Index(false))
                        .Date(d => d.Name(e => e.Timestamp)))))
            ;

            if (!create.IsValid)
            {
                _logger.LogError("Failed to create index '{Index}': {Error}", IndexName, create.DebugInformation);
                throw new ElasticsearchClientException($"Cannot create index {IndexName}: {create.DebugInformation}");
            }
        }

        public async Task<long> AppendEventAsync(IBpmnEvent @event, CancellationToken cancellationToken = default)
        {
            if (@event == null) throw new ArgumentNullException(nameof(@event));
            
            var serialized = _serializer.Serialize(@event);
            return await AppendEventAsync(serialized, cancellationToken);
        }

        public async Task<long> AppendEventsAsync(IEnumerable<IBpmnEvent> events, CancellationToken cancellationToken = default)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            
            var serialized = events.Select(_serializer.Serialize).ToList();
            return await AppendEventsAsync(serialized, cancellationToken);
        }

        private async Task<long> AppendEventAsync(SerializedEvent @event, CancellationToken cancellationToken = default)
        {
            var resp = await _client.IndexAsync(@event, i => i
                .Index(IndexName)
                .Id(@event.Id)
                .Refresh(Refresh.True),
                cancellationToken);
            
            if (!resp.IsValid)
                throw new ElasticsearchClientException($"AppendEventAsync failed: {resp.DebugInformation}");

            // Simply return and increment our position counter
            return Interlocked.Increment(ref _lastPosition);
        }

        private async Task<long> AppendEventsAsync(IEnumerable<SerializedEvent> events, CancellationToken cancellationToken = default)
        {
            var list = events.ToList();
            if (!list.Any()) return 0;

            var bulkDescriptor = new BulkDescriptor();
            foreach (var doc in list)
            {
                bulkDescriptor.Index<SerializedEvent>(i => i
                    .Document(doc)
                    .Index(IndexName)
                    .Id(doc.Id));
            }

            var bulk = await _client.BulkAsync(bulkDescriptor
                .Refresh(Refresh.True), cancellationToken);

            if (!bulk.IsValid)
                throw new ElasticsearchClientException($"AppendEventsAsync failed: {bulk.DebugInformation}");

            // Return the last position after incrementing for each item
            long finalPosition = _lastPosition;
            for (int i = 0; i < list.Count; i++)
            {
                finalPosition = Interlocked.Increment(ref _lastPosition);
            }
            
            return finalPosition;
        }

        public async Task<IReadOnlyList<IBpmnEvent>> ReadEventsAsync(
            long position = 0,
            int count = 100,
            Func<IBpmnEvent, bool>? predicate = null,
            CancellationToken cancellationToken = default)
        {
            var serializedEvents = await ReadSerializedEventsAsync(position, count, null, cancellationToken);
            var events = serializedEvents.Select(_serializer.Deserialize).ToList();
            
            return predicate != null ? events.Where(predicate).ToList() : events;
        }

        private async Task<IReadOnlyList<SerializedEvent>> ReadSerializedEventsAsync(
            long position = 0,
            int count = 100,
            Func<SerializedEvent, bool>? predicate = null,
            CancellationToken cancellationToken = default)
        {
            var resp = await _client.SearchAsync<SerializedEvent>(s => s
                .Index(IndexName)
                .Sort(ss => ss.Ascending(e => e.Timestamp))
                .From((int)position)
                .Size(count),
                cancellationToken);

            if (!resp.IsValid)
                throw new ElasticsearchClientException($"ReadEventsAsync failed: {resp.DebugInformation}");

            var docs = resp.Documents;
            return predicate != null ? docs.Where(predicate).ToList() : docs.ToList();
        }

        public async Task<IReadOnlyList<IBpmnEvent>> ReadProcessInstanceEventsAsync(
            string processInstanceId,
            long position = 0,
            int count = 100,
            Func<IBpmnEvent, bool>? predicate = null,
            CancellationToken cancellationToken = default)
        {
            var serializedEvents = await ReadProcessInstanceSerializedEventsAsync(
                processInstanceId, position, count, null, cancellationToken);
                
            var events = serializedEvents.Select(_serializer.Deserialize).ToList();
            
            return predicate != null ? events.Where(predicate).ToList() : events;
        }

        private async Task<IReadOnlyList<SerializedEvent>> ReadProcessInstanceSerializedEventsAsync(
            string processInstanceId,
            long position = 0,
            int count = 100,
            Func<SerializedEvent, bool>? predicate = null,
            CancellationToken cancellationToken = default)
        {
            var resp = await _client.SearchAsync<SerializedEvent>(s => s
                .Index(IndexName)
                .Sort(ss => ss.Ascending(e => e.Timestamp))
                .Query(q => q.Term(t => t.Field(f => f.ProcessInstanceId).Value(processInstanceId)))
                .From((int)position)
                .Size(count),
                cancellationToken);

            if (!resp.IsValid)
                throw new ElasticsearchClientException($"ReadProcessInstanceEventsAsync failed: {resp.DebugInformation}");

            var docs = resp.Documents;
            return predicate != null ? docs.Where(predicate).ToList() : docs.ToList();
        }

        public Task<string> SubscribeToEventsAsync(
            Func<IBpmnEvent, Task> handler,
            Func<IBpmnEvent, bool>? predicate = null,
            long position = 0,
            CancellationToken cancellationToken = default)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            
            var subId = Guid.NewGuid().ToString();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _subscriptions[subId] = cts;

            _ = Task.Run(async () =>
            {
                var lastPos = position;
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        var batch = await ReadSerializedEventsAsync(lastPos, 50, null, cts.Token);
                        foreach (var serializedEvent in batch)
                        {
                            lastPos++;
                            
                            try
                            {
                                var @event = _serializer.Deserialize(serializedEvent);
                                
                                if (predicate == null || predicate(@event))
                                    await handler(@event);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error deserializing or handling event");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in subscription '{SubId}'", subId);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
                }
            }, cts.Token);

            return Task.FromResult(subId);
        }

        public Task UnsubscribeAsync(string subscriptionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(subscriptionId))
                throw new ArgumentNullException(nameof(subscriptionId));
                
            if (_subscriptions.TryRemove(subscriptionId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
            return Task.CompletedTask;
        }

        #region IEventQueryService Implementation

        public async Task<IReadOnlyList<IBpmnEvent>> QueryByTypeNameAsync(
            string typeName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentNullException(nameof(typeName));
                
            var serializedEvents = await QuerySerializedByTypeNameAsync(typeName, cancellationToken);
            return serializedEvents.Select(_serializer.Deserialize).ToList();
        }
        
        private async Task<IReadOnlyList<SerializedEvent>> QuerySerializedByTypeNameAsync(
            string typeName,
            CancellationToken cancellationToken = default)
        {
            var resp = await _client.SearchAsync<SerializedEvent>(s => s
                .Index(IndexName)
                .Size(1000)
                .Query(q => q.Term(t => t.Field(f => f.TypeName).Value(typeName))),
                cancellationToken);

            if (!resp.IsValid)
                throw new ElasticsearchClientException($"QueryByTypeNameAsync failed: {resp.DebugInformation}");

            return resp.Documents.ToList();
        }

        public async Task<IReadOnlyList<IBpmnEvent>> QueryByNamespaceAsync(
            string @namespace,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(@namespace))
                throw new ArgumentNullException(nameof(@namespace));
                
            var serializedEvents = await QuerySerializedByNamespaceAsync(@namespace, cancellationToken);
            return serializedEvents.Select(_serializer.Deserialize).ToList();
        }
        
        private async Task<IReadOnlyList<SerializedEvent>> QuerySerializedByNamespaceAsync(
            string @namespace,
            CancellationToken cancellationToken = default)
        {
            var resp = await _client.SearchAsync<SerializedEvent>(s => s
                .Index(IndexName)
                .Size(1000)
                .Query(q => q.Term(t => t.Field(f => f.Namespace).Value(@namespace))),
                cancellationToken);

            if (!resp.IsValid)
                throw new ElasticsearchClientException($"QueryByNamespaceAsync failed: {resp.DebugInformation}");

            return resp.Documents.ToList();
        }
        
        public async Task<IReadOnlyList<IBpmnEvent>> QueryByProcessInstanceIdAsync(
            string processInstanceId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(processInstanceId))
                throw new ArgumentNullException(nameof(processInstanceId));
                
            var serializedEvents = await QuerySerializedByProcessInstanceIdAsync(processInstanceId, cancellationToken);
            return serializedEvents.Select(_serializer.Deserialize).ToList();
        }
        
        private async Task<IReadOnlyList<SerializedEvent>> QuerySerializedByProcessInstanceIdAsync(
            string processInstanceId,
            CancellationToken cancellationToken = default)
        {
            var resp = await _client.SearchAsync<SerializedEvent>(s => s
                .Index(IndexName)
                .Size(1000)
                .Query(q => q.Term(t => t.Field(f => f.ProcessInstanceId).Value(processInstanceId))),
                cancellationToken);

            if (!resp.IsValid)
                throw new ElasticsearchClientException($"QueryByProcessInstanceIdAsync failed: {resp.DebugInformation}");

            return resp.Documents.ToList();
        }
        
        public async Task<IReadOnlyList<IBpmnEvent>> QueryAsync(
            Func<IBpmnEvent, bool> predicate,
            CancellationToken cancellationToken = default)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
                
            var resp = await _client.SearchAsync<SerializedEvent>(s => s
                .Index(IndexName)
                .Size(1000)
                .Query(q => q.MatchAll()),
                cancellationToken);

            if (!resp.IsValid)
                throw new ElasticsearchClientException($"QueryAsync failed: {resp.DebugInformation}");
                
            var events = resp.Documents
                .Select(_serializer.Deserialize)
                .Where(predicate)
                .ToList();
                
            return events;
        }

        public async Task<IReadOnlyList<IBpmnEvent>> ReadEventsTimeRangeAsync(
            DateTime fromTimestamp, 
            DateTime? toTimestamp, 
            Func<IBpmnEvent, bool>? filter = null,
            CancellationToken cancellationToken = default)
        {
            var searchDescriptor = new SearchDescriptor<SerializedEvent>()
                .Index(IndexName)
                .Size(1000)
                .Sort(s => s.Ascending(f => f.Timestamp));

            // Add timestamp range query
            searchDescriptor = searchDescriptor.Query(q => q
                .Bool(b => b
                    .Must(
                        m => m.DateRange(r => r
                            .Field(f => f.Timestamp)
                            .GreaterThanOrEquals(fromTimestamp)
                            .LessThanOrEquals(toTimestamp ?? DateTime.UtcNow)
                        )
                    )
                )
            );

            var response = await _client.SearchAsync<SerializedEvent>(searchDescriptor, cancellationToken);
            if (!response.IsValid)
            {
                throw new Exception($"Failed to read events: {response.DebugInformation}");
            }

            var events = new List<IBpmnEvent>();
            foreach (var hit in response.Hits)
            {
                try
                {
                    var @event = _serializer.Deserialize(hit.Source);
                    
                    // Apply filter if provided
                    if (filter == null || filter(@event))
                    {
                        events.Add(@event);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize event {EventId}", hit.Source.Id);
                }
            }

            return events;
        }

        #endregion
    }
}
