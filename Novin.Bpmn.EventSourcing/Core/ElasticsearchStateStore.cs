using Microsoft.Extensions.Logging;
using Nest;
using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using System.Reflection;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// Custom exception for concurrency conflicts
/// </summary>
public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
    public ConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Implementation of state store using Elasticsearch
/// </summary>
public class ElasticsearchStateStore : IStateStore
{
    private readonly IElasticClient _elasticClient;
    private readonly ILogger<ElasticsearchStateStore> _logger;
    private const string IndexPrefix = "bpmn-states-";
    private const string ProcessInstanceIdField = "processInstanceId";
    private const string VersionField = "version";
    private const string StateField = "state";
    private const string CreatedAtField = "createdAt";
    private const string UpdatedAtField = "updatedAt";
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _indexLock = new(1, 1);

    public ElasticsearchStateStore(
        IElasticClient elasticClient,
        ILogger<ElasticsearchStateStore> logger)
    {
        _elasticClient = elasticClient ?? throw new ArgumentNullException(nameof(elasticClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        
        EnsureIndexTemplateAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureIndexTemplateAsync()
    {
        try
        {
            await _indexLock.WaitAsync();
            try
            {
                var templateName = IndexPrefix + "template";
                var templateExists = await _elasticClient.Indices.TemplateExistsAsync(templateName);
                if (!templateExists.Exists)
                {
                    var response = await _elasticClient.Indices.PutTemplateAsync(templateName, t => t
                        .Mappings(m => m
                            .Map<dynamic>(tm => tm
                                .Properties(p => p
                                    .Keyword(k => k.Name(ProcessInstanceIdField))
                                    .Number(n => n.Name(VersionField).Type(NumberType.Long))
                                    .Keyword(k => k.Name("fullName"))
                                    .Keyword(k => k.Name("assemblyName"))
                                    .Keyword(k => k.Name("namespaceName"))
                                    .Object<dynamic>(o => o.Name("payload").Dynamic())
                                    .Date(d => d.Name(CreatedAtField))
                                    .Date(d => d.Name(UpdatedAtField)))))
                        .Settings(s => s
                            .NumberOfShards(3)
                            .NumberOfReplicas(1)
                            .RefreshInterval("1s")
                            .Analysis(a => a
                                .Normalizers(n => n
                                    .Custom("lowercase_normalizer", c => c
                                        .Filters("lowercase")))))
                        .IndexPatterns(IndexPrefix + "*"));

                    if (!response.IsValid)
                    {
                        throw new ElasticsearchException($"Failed to create index template: {response.DebugInformation}");
                    }
                }
            }
            finally
            {
                _indexLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure index template exists");
            throw;
        }
    }

    private async Task EnsureIndexExistsAsync(string indexName)
    {
        try
        {
            var indexExists = await _elasticClient.Indices.ExistsAsync(indexName);
            if (!indexExists.Exists)
            {
                var response = await _elasticClient.Indices.CreateAsync(indexName, c => c
                    .Settings(s => s
                        .NumberOfShards(3)
                        .NumberOfReplicas(1)
                        .RefreshInterval("1s")));

                if (!response.IsValid)
                {
                    throw new ElasticsearchException($"Failed to create index {indexName}: {response.DebugInformation}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure index {IndexName} exists", indexName);
            throw;
        }
    }

    public async Task<BpmnProcessState?> GetStateAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be empty", nameof(processInstanceId));

        try
        {
            var indexName = GetIndexName(processInstanceId);
            await EnsureIndexExistsAsync(indexName);

            var response = await _elasticClient.GetAsync<dynamic>(processInstanceId, g => g
                .Index(indexName),
                cancellationToken);

            if (!response.IsValid)
            {
                if (response.ApiCall.HttpStatusCode == 404)
                {
                    return null;
                }
                throw new ElasticsearchException($"Failed to get state: {response.DebugInformation}");
            }

            var sourceDict = (IDictionary<string, object>)response.Source;
            var payload = sourceDict["payload"].ToString();
            var fullName = sourceDict["fullName"].ToString();
            var assemblyName = sourceDict["assemblyName"].ToString();

            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);
            
            if (assembly == null)
            {
                assembly = Assembly.Load(assemblyName);
            }

            var stateType = assembly.GetType(fullName);
            if (stateType == null)
            {
                throw new ElasticsearchException($"State type {fullName} not found in assembly {assemblyName}");
            }

            var state = JsonSerializer.Deserialize(payload, stateType, _jsonOptions) as BpmnProcessState;
            if (state == null)
            {
                throw new ElasticsearchException($"Failed to deserialize state for process instance {processInstanceId}");
            }

            return state;
        }
        catch (Exception ex) when (ex is not ElasticsearchException)
        {
            _logger.LogError(ex, "Error getting state for process instance {ProcessInstanceId}", processInstanceId);
            throw new ElasticsearchException("Failed to get state", ex);
        }
    }

    public async Task<(BpmnProcessState? State, long Version)> GetStateWithVersionAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be empty", nameof(processInstanceId));

        try
        {
            var indexName = GetIndexName(processInstanceId);
            await EnsureIndexExistsAsync(indexName);

            var response = await _elasticClient.GetAsync<dynamic>(processInstanceId, g => g
                .Index(indexName),
                cancellationToken);

            if (!response.IsValid)
            {
                if (response.ApiCall.HttpStatusCode == 404)
                {
                    return (null, 0);
                }
                throw new ElasticsearchException($"Failed to get state: {response.DebugInformation}");
            }

            var stateJson = response.Source.state.ToString();
            var state = JsonSerializer.Deserialize<BpmnProcessState>(stateJson, _jsonOptions);
            
            if (state == null)
            {
                throw new ElasticsearchException($"Failed to deserialize state for process instance {processInstanceId}");
            }

            var version = (long)response.SequenceNumber;
            return (state, version);
        }
        catch (Exception ex) when (ex is not ElasticsearchException)
        {
            _logger.LogError(ex, "Error getting state for process instance {ProcessInstanceId}", processInstanceId);
            throw new ElasticsearchException("Failed to get state", ex);
        }
    }

    public async Task<long> SaveStateAsync(string processInstanceId, BpmnProcessState state, long? expectedVersion = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be empty", nameof(processInstanceId));
            
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        try
        {
            var indexName = GetIndexName(processInstanceId);
            await EnsureIndexExistsAsync(indexName);

            // First check if the document exists and get its version
            var existsResponse = await _elasticClient.DocumentExistsAsync<dynamic>(processInstanceId, d => d
                .Index(indexName),
                cancellationToken);

            if (existsResponse.Exists)
            {
                // If document exists but no expected version provided, get current version
                if (!expectedVersion.HasValue)
                {
                    var getResponse = await _elasticClient.GetAsync<dynamic>(processInstanceId, g => g
                        .Index(indexName),
                        cancellationToken);

                    if (!getResponse.IsValid)
                    {
                        throw new ElasticsearchException($"Failed to get current version: {getResponse.DebugInformation}");
                    }

                    expectedVersion = (long)getResponse.SequenceNumber;
                }
            }
            else if (expectedVersion.HasValue)
            {
                // If document doesn't exist but expected version is provided, this is a conflict
                throw new ConcurrencyException($"Process instance {processInstanceId} does not exist but expected version {expectedVersion} was provided");
            }

            var stateType = state.GetType();
            var now = DateTime.UtcNow;
            var document = new
            {
                processInstanceId,
                version = expectedVersion.HasValue ? expectedVersion.Value + 1 : 1,
                fullName = stateType.FullName,
                assemblyName = stateType.Assembly.GetName().Name,
                namespaceName = stateType.Namespace,
                payload = JsonSerializer.Serialize(state, _jsonOptions),
                createdAt = now,
                updatedAt = now
            };

            var response = await _elasticClient.IndexAsync(document, i => i
                .Index(indexName)
                .Id(processInstanceId)
                .IfSequenceNumber(expectedVersion)
                .IfPrimaryTerm(expectedVersion.HasValue ? 1 : null)
                .Refresh(Refresh.True),
                cancellationToken);

            if (!response.IsValid)
            {
                if (response.ApiCall.HttpStatusCode == 409)
                {
                    throw new ConcurrencyException($"Concurrency conflict for process instance {processInstanceId}");
                }
                throw new ElasticsearchException($"Failed to save state: {response.DebugInformation}");
            }

            return (long)response.SequenceNumber;
        }
        catch (ConcurrencyException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not ElasticsearchException)
        {
            _logger.LogError(ex, "Error saving state for process instance {ProcessInstanceId}", processInstanceId);
            throw new ElasticsearchException("Failed to save state", ex);
        }
    }

    public async Task DeleteStateAsync(string processInstanceId, long? expectedVersion = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be empty", nameof(processInstanceId));

        try
        {
            var indexName = GetIndexName(processInstanceId);
            await EnsureIndexExistsAsync(indexName);

            var response = await _elasticClient.DeleteAsync<dynamic>(processInstanceId, d => d
                .Index(indexName)
                .IfSequenceNumber(expectedVersion)
                .IfPrimaryTerm(expectedVersion.HasValue ? 1 : null)
                .Refresh(Refresh.True),
                cancellationToken);

            if (!response.IsValid)
            {
                if (response.ApiCall.HttpStatusCode == 404)
                {
                    return;
                }
                if (response.ApiCall.HttpStatusCode == 409)
                {
                    throw new ConcurrencyException($"Concurrency conflict for process instance {processInstanceId}");
                }
                throw new ElasticsearchException($"Failed to delete state: {response.DebugInformation}");
            }
        }
        catch (ConcurrencyException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not ElasticsearchException)
        {
            _logger.LogError(ex, "Error deleting state for process instance {ProcessInstanceId}", processInstanceId);
            throw new ElasticsearchException("Failed to delete state", ex);
        }
    }

    public async Task<bool> HasStateAsync(string processInstanceId)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be empty", nameof(processInstanceId));

        try
        {
            var indexName = GetIndexName(processInstanceId);
            await EnsureIndexExistsAsync(indexName);

            var response = await _elasticClient.DocumentExistsAsync<dynamic>(processInstanceId, d => d
                .Index(indexName));

            return response.Exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking state existence for process instance {ProcessInstanceId}", processInstanceId);
            throw new ElasticsearchException("Failed to check state existence", ex);
        }
    }

    public async Task<long> GetVersionAsync(string processInstanceId)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new ArgumentException("Process instance ID cannot be empty", nameof(processInstanceId));

        try
        {
            var indexName = GetIndexName(processInstanceId);
            await EnsureIndexExistsAsync(indexName);

            var response = await _elasticClient.GetAsync<dynamic>(processInstanceId, g => g
                .Index(indexName));

            if (!response.IsValid)
            {
                if (response.ApiCall.HttpStatusCode == 404)
                {
                    return -1;
                }
                throw new ElasticsearchException($"Failed to get version: {response.DebugInformation}");
            }

            return (long)response.SequenceNumber;
        }
        catch (Exception ex) when (ex is not ElasticsearchException)
        {
            _logger.LogError(ex, "Error getting version for process instance {ProcessInstanceId}", processInstanceId);
            throw new ElasticsearchException("Failed to get version", ex);
        }
    }

    public async Task<List<BpmnProcessState>> FindStatesByPatternAsync(
        string pattern,
        Func<BpmnProcessState, bool>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(pattern))
            throw new ArgumentException("Pattern cannot be empty", nameof(pattern));

        try
        {
            var searchResponse = await _elasticClient.SearchAsync<dynamic>(s => s
                .Index(IndexPrefix + "*")
                .Query(q => q
                    .Bool(b => b
                        .Must(m => m
                            .Wildcard(w => w
                                .Field(ProcessInstanceIdField)
                                .Value(pattern)))))
                .Size(1000)
                .Sort(sort => sort
                    .Descending(UpdatedAtField)),
                cancellationToken);

            if (!searchResponse.IsValid)
            {
                throw new ElasticsearchException($"Failed to search states: {searchResponse.DebugInformation}");
            }

            var states = new List<BpmnProcessState>();
            foreach (var hit in searchResponse.Hits)
            {
                try
                {
                    var stateJson = hit.Source.state.ToString();
                    var state = JsonSerializer.Deserialize<BpmnProcessState>(stateJson, _jsonOptions);
                    if (state != null && (predicate == null || predicate(state)))
                    {
                        states.Add(state);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing state from hit {HitId}", hit.Id);
                }
            }

            return states;
        }
        catch (Exception ex) when (ex is not ElasticsearchException)
        {
            _logger.LogError(ex, "Error finding states with pattern {Pattern}", pattern);
            throw new ElasticsearchException("Failed to find states", ex);
        }
    }

    private string GetIndexName(string processInstanceId)
    {
        return $"{IndexPrefix}{DateTime.UtcNow:yyyy.MM}";
    }
} 