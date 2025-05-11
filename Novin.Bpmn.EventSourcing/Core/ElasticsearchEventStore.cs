using Microsoft.Extensions.Logging;
using Nest;
using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using System.Reflection;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// Elasticsearch-based implementation of IEventStore
/// </summary>
public class ElasticsearchEventStore : IEventStore
{
    private readonly IElasticClient _elasticClient;
    private readonly ILogger<ElasticsearchEventStore> _logger;
    private const string IndexPrefix = "bpmn-events-";
    private const string EventTypeField = "eventType";
    private const string ProcessInstanceIdField = "processInstanceId";
    private const string PositionField = "position";
    private const string TimestampField = "timestamp";
    private const string DataField = "data";
    private const string ProcessedField = "processed";
    private const string ProcessedAtField = "processedAt";
    private const string ProcessorIdField = "processorId";
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _positionLock = new(1, 1);

    public ElasticsearchEventStore(
        IElasticClient elasticClient,
        ILogger<ElasticsearchEventStore> logger)
    {
        _elasticClient = elasticClient ?? throw new ArgumentNullException(nameof(elasticClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        
        // Ensure index template exists with retry
        var retryCount = 3;
        var delay = TimeSpan.FromSeconds(2);
        
        for (var i = 0; i < retryCount; i++)
        {
            try
            {
                EnsureIndexTemplateAsync().GetAwaiter().GetResult();
                break;
            }
            catch (Exception ex) when (i < retryCount - 1)
            {
                _logger.LogWarning(ex, "Failed to ensure index template exists, attempt {Attempt} of {MaxAttempts}. Retrying in {Delay} seconds...", 
                    i + 1, retryCount, delay.TotalSeconds);
                Thread.Sleep(delay);
                delay *= 2; // Exponential backoff
            }
        }
    }

    private async Task EnsureIndexTemplateAsync()
    {
        try
        {
            var templateName = IndexPrefix + "template";
            var templateExists = await _elasticClient.Indices.TemplateExistsAsync(templateName);
            if (!templateExists.Exists)
            {
                var response = await _elasticClient.Indices.PutTemplateAsync(templateName, t => t
                    .Mappings(m => m
                        .Map<dynamic>(tm => tm
                            .AutoMap()
                            .Properties(p => p
                                .Keyword(k => k.Name(EventTypeField))
                                .Keyword(k => k.Name(ProcessInstanceIdField))
                                .Number(n => n.Name(PositionField).Type(NumberType.Long))
                                .Date(d => d.Name(TimestampField))
                                .Boolean(b => b.Name(ProcessedField))
                                .Date(d => d.Name(ProcessedAtField))
                                .Keyword(k => k.Name(ProcessorIdField))
                                .Object<dynamic>(o => o.Name(DataField).Dynamic()))))
                    .Settings(s => s
                        .NumberOfShards(1)
                        .NumberOfReplicas(0)
                        .RefreshInterval("1s"))
                    .IndexPatterns(IndexPrefix + "*"));

                if (!response.IsValid)
                {
                    throw new ElasticsearchException($"Failed to create index template: {response.DebugInformation}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure index template exists");
            throw;
        }
    }

    public async Task<long> AppendEventAsync(IBpmnEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event == null) throw new ArgumentNullException(nameof(@event));

        try
        {
            var indexName = GetIndexName(@event.Timestamp);
            await EnsureIndexExistsAsync(indexName, cancellationToken);
            
            await _positionLock.WaitAsync(cancellationToken);
            try
            {
                var position = await GetNextPositionAsync(indexName, cancellationToken);

                var eventType = @event.GetType();
                var document = new
                {
                   
                        fullName = eventType.FullName,
                        assemblyName = eventType.Assembly.GetName().Name,
                        namespaceName = eventType.Namespace
                    ,
                    processInstanceId = @event.ProcessInstanceId,
                    position = position,
                    timestamp = @event.Timestamp,
                    id = @event.EventId,
                    intent = @event.Intent,
                    processed = false,
                    processedAt = (DateTime?)null,
                    processorId = (string?)null,
                    payload = JsonSerializer.Serialize(@event, _jsonOptions)
                };

                var response = await _elasticClient.IndexAsync(document, i => i
                    .Index(indexName)
                    .Id(@event.EventId.ToString())
                    .Refresh(Refresh.True),
                    cancellationToken
                );

                if (!response.IsValid)
                {
                    throw new ElasticsearchException($"Failed to append event: {response.DebugInformation}");
                }

                _logger.LogDebug("Appended event {EventId} at position {Position}", @event.EventId, position);
                return position;
            }
            finally
            {
                _positionLock.Release();
            }
        }
        catch (Exception ex) when (ex is not ElasticsearchException)
        {
            _logger.LogError(ex, "Failed to append event {EventId}", @event.EventId);
            throw new ElasticsearchException("Failed to append event", ex);
        }
    }

    public async Task<long> AppendEventsAsync(IEnumerable<IBpmnEvent> events, CancellationToken cancellationToken = default)
    {
        if (events == null) throw new ArgumentNullException(nameof(events));
        var eventList = events.ToList();
        if (!eventList.Any()) return 0;

        try
        {
            var bulkDescriptor = new BulkDescriptor();
            var indexName = GetIndexName(DateTime.UtcNow);
            await EnsureIndexExistsAsync(indexName, cancellationToken);

            await _positionLock.WaitAsync(cancellationToken);
            try
            {
                var position = await GetNextPositionAsync(indexName, cancellationToken);
                var documents = new List<object>();

                foreach (var @event in eventList)
                {
                    var eventType = @event.GetType();
                    var document = new
                    {
                        
                            fullName = eventType.FullName,
                            assemblyName = eventType.Assembly.GetName().Name,
                            namespaceName = eventType.Namespace
                        ,
                        processInstanceId = @event.ProcessInstanceId,
                        position = position++,
                        timestamp = @event.Timestamp,
                        id = @event.EventId,
                        intent = @event.Intent,
                        processed = false,
                        processedAt = (DateTime?)null,
                        processorId = (string?)null,
                        payload = JsonSerializer.Serialize(@event, _jsonOptions)
                    };

                    bulkDescriptor.Index<object>(i => i
                        .Index(indexName)
                        .Id(@event.EventId.ToString())
                        .Document(document));
                }

                var response = await _elasticClient.BulkAsync(bulkDescriptor, cancellationToken);
                if (!response.IsValid)
                {
                    throw new ElasticsearchException($"Failed to append events: {response.DebugInformation}");
                }

                if (response.Errors)
                {
                    var errors = response.ItemsWithErrors
                        .Select(i => new { Id = i.Id, Error = i.Error?.Reason })
                        .Where(e => e.Error != null);
                    
                    var errorMessage = string.Join(", ", errors.Select(e => $"Event {e.Id}: {e.Error}"));
                    throw new ElasticsearchException($"Bulk operation had errors: {errorMessage}");
                }

                _logger.LogDebug("Appended {Count} events starting at position {Position}", eventList.Count, position - eventList.Count);
                return position - 1;
            }
            finally
            {
                _positionLock.Release();
            }
        }
        catch (Exception ex) when (ex is not ElasticsearchException)
        {
            _logger.LogError(ex, "Failed to append events");
            throw new ElasticsearchException("Failed to append events", ex);
        }
    }

    private async Task EnsureIndexExistsAsync(string indexName, CancellationToken cancellationToken)
    {
        var exists = await _elasticClient.Indices.ExistsAsync(indexName, ct: cancellationToken);
        if (!exists.Exists)
        {
            var response = await _elasticClient.Indices.CreateAsync(indexName, c => c
                .Settings(s => s
                    .NumberOfShards(1)
                    .NumberOfReplicas(0)
                    .RefreshInterval("1s")),
                cancellationToken);

            if (!response.IsValid)
            {
                throw new ElasticsearchException($"Failed to create index {indexName}: {response.DebugInformation}");
            }
        }
    }

    public async Task<List<IBpmnEvent>> ReadEventsAsync(
        long position = 0,
        int count = 100,
        Func<IBpmnEvent, bool>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var searchResponse = await _elasticClient.SearchAsync<dynamic>(s => s
                .Index(IndexPrefix + "*")
                .Query(q => q
                    .Bool(b => b
                        .Must(m => m
                            .Range(r => r
                                .Field(PositionField)
                                .GreaterThanOrEquals(position)
                            ),
                        m => m
                            .Term(t => t
                                .Field(ProcessedField)
                                .Value(false)
                            )
                        )
                    )
                )
                .Sort(sort => sort
                    .Ascending(PositionField)
                )
                .Size(count),
                cancellationToken
            );

            if (!searchResponse.IsValid)
            {
                throw new ElasticsearchException($"Failed to read events: {searchResponse.DebugInformation}");
            }

            return await DeserializeEventsAsync(searchResponse.Hits, predicate);
        }
        catch (Exception ex) when (ex is not ElasticsearchException)
        {
            _logger.LogError(ex, "Failed to read events from position {Position}", position);
            throw new ElasticsearchException("Failed to read events", ex);
        }
    }

    public async Task<List<IBpmnEvent>> ReadProcessInstanceEventsAsync(
        string processInstanceId,
        long position = 0,
        int count = 100,
        Func<IBpmnEvent, bool>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be null or empty", nameof(processInstanceId));

        try
        {
            var searchResponse = await _elasticClient.SearchAsync<dynamic>(s => s
                .Index(IndexPrefix + "*")
                .Query(q => q
                    .Bool(b => b
                        .Must(m => m
                            .Term(t => t.Field(ProcessInstanceIdField).Value(processInstanceId)),
                        m => m
                            .Range(r => r
                                .Field(PositionField)
                                .GreaterThanOrEquals(position)
                            )
                        )
                    )
                )
                .Sort(sort => sort
                    .Ascending(PositionField)
                )
                .Size(count),
                cancellationToken
            );

            if (!searchResponse.IsValid)
            {
                throw new ElasticsearchException($"Failed to read process instance events: {searchResponse.DebugInformation}");
            }

            return await DeserializeEventsAsync(searchResponse.Hits, predicate);
        }
        catch (Exception ex) when (ex is not ElasticsearchException)
        {
            _logger.LogError(ex, "Failed to read events for process instance {ProcessInstanceId}", processInstanceId);
            throw new ElasticsearchException("Failed to read process instance events", ex);
        }
    }

    private async Task<List<IBpmnEvent>> DeserializeEventsAsync(IReadOnlyCollection<IHit<dynamic>> hits, Func<IBpmnEvent, bool>? predicate)
    {
        var events = new List<IBpmnEvent>();
        foreach (var hit in hits)
        {
            try
            {
                var sourceDict = (IDictionary<string, object>)hit.Source;
                var payload = sourceDict["payload"].ToString();
                var fullName = sourceDict["fullName"].ToString();
                var assemblyName = sourceDict["assemblyName"].ToString();

                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == assemblyName);
                
                if (assembly == null)
                {
                    assembly = Assembly.Load(assemblyName);
                }

                var eventTypeObj = assembly.GetType(fullName);
                
                if (eventTypeObj != null)
                {
                    try
                    {
                        var @event = JsonSerializer.Deserialize(payload, eventTypeObj, _jsonOptions) as IBpmnEvent;
                        if (@event != null && (predicate == null || predicate(@event)))
                        {
                            events.Add(@event);
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, 
                            "Failed to deserialize event payload for type {EventType}. Payload: {Payload}", 
                            fullName, payload);
                    }
                }
                else
                {
                    LoggerExtensions.LogWarning(_logger, "Event type {EventType} not found in assembly {Assembly}", fullName, assemblyName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize event from hit {HitId}", hit.Id);
            }
        }
        return events;
    }

    public async Task<string> SubscribeToEventsAsync(
        Func<IBpmnEvent, Task> handler,
        Func<IBpmnEvent, bool>? predicate = null,
        long position = 0,
        CancellationToken cancellationToken = default)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        try
        {
            var subscriptionId = Guid.NewGuid().ToString();
            var searchResponse = await _elasticClient.SearchAsync<dynamic>(s => s
                .Index(IndexPrefix + "*")
                .Query(q => q
                    .Bool(b => b
                        .Must(m => m
                            .Range(r => r
                                .Field(PositionField)
                                .GreaterThan(position - 1)
                            )
                        )
                    )
                )
                .Sort(sort => sort
                    .Ascending(PositionField)
                )
                .Scroll("5m"),
                cancellationToken
            );

            if (!searchResponse.IsValid)
            {
                throw new ElasticsearchException($"Failed to subscribe to events: {searchResponse.DebugInformation}");
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var scrollId = searchResponse.ScrollId;
                    var searchResults = searchResponse.Hits;

                    while (searchResults.Any() && !cancellationToken.IsCancellationRequested)
                    {
                        var events = await DeserializeEventsAsync(searchResults, predicate);
                        foreach (var @event in events)
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            await handler(@event);
                        }

                        var scrollResponse = await _elasticClient.ScrollAsync<dynamic>(new ScrollRequest(scrollId, "5m"), cancellationToken);

                        if (!scrollResponse.IsValid)
                        {
                            throw new ElasticsearchException($"Failed to scroll events: {scrollResponse.DebugInformation}");
                        }

                        scrollId = scrollResponse.ScrollId;
                        searchResults = scrollResponse.Hits;
                    }

                    await _elasticClient.ClearScrollAsync(c => c.ScrollId(scrollId), cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error in event subscription {SubscriptionId}", subscriptionId);
                }
            }, cancellationToken);

            return subscriptionId;
        }
        catch (Exception ex) when (ex is not ElasticsearchException)
        {
            _logger.LogError(ex, "Failed to subscribe to events");
            throw new ElasticsearchException("Failed to subscribe to events", ex);
        }
    }

    public async Task UnsubscribeAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        // Note: In Elasticsearch, we don't need to do anything special to unsubscribe
        // The subscription will automatically end when the scroll expires
        await Task.CompletedTask;
    }

    private string GetIndexName(DateTime timestamp)
    {
        return $"{IndexPrefix}{timestamp:yyyy.MM}";
    }

    private async Task<long> GetNextPositionAsync(string indexName, CancellationToken cancellationToken)
    {
        var searchResponse = await _elasticClient.SearchAsync<dynamic>(s => s
            .Index(indexName)
            .Size(0)
            .Aggregations(a => a
                .Max("max_position", m => m
                    .Field(PositionField)
                )
            ),
            cancellationToken
        );

        if (!searchResponse.IsValid)
        {
            throw new ElasticsearchException($"Failed to get next position: {searchResponse.DebugInformation}");
        }

        var maxPosition = searchResponse.Aggregations.Max("max_position");
        return (long)(maxPosition?.Value ?? 0) + 1;
    }

    public async Task MarkAsProcessedAsync(string eventId, string processorId, CancellationToken cancellationToken = default)
    {
        try
        {
            var updateResponse = await _elasticClient.UpdateAsync<dynamic>(eventId, u => u
                .Index(IndexPrefix + "*")
                .Doc(new
                {
                    processed = true,
                    processedAt = DateTime.UtcNow,
                    processorId = processorId
                })
                .Refresh(Refresh.True),
                cancellationToken);

            if (!updateResponse.IsValid)
            {
                throw new ElasticsearchException($"Failed to mark event as processed: {updateResponse.DebugInformation}");
            }

            _logger.LogDebug("Marked event {EventId} as processed by {ProcessorId}", eventId, processorId);
        }
        catch (Exception ex) when (ex is not ElasticsearchException)
        {
            _logger.LogError(ex, "Failed to mark event {EventId} as processed", eventId);
            throw new ElasticsearchException("Failed to mark event as processed", ex);
        }
    }
}

public class ElasticsearchException : Exception
{
    public ElasticsearchException(string message) : base(message) { }
    public ElasticsearchException(string message, Exception innerException) : base(message, innerException) { }
} 