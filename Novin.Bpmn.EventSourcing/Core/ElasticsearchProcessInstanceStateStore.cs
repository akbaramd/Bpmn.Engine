using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Microsoft.Extensions.Logging;
using Nest;
using Newtonsoft.Json;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Models;

namespace Novin.Bpmn.EventSourcing.Core
{
    // Implementing the interface correctly
    public partial class ElasticsearchProcessInstanceStateStore : IProcessInstanceStateStore
    {
        private const string IndexName = "bpmn-process-instance-states";
        private const string StateIndexName = "bpmn-process-instance-generic-states";
        private readonly IElasticClient _elasticClient;
        private readonly ILogger<ElasticsearchProcessInstanceStateStore> _logger;
        private readonly JsonSerializerSettings _jsonSettings;

        public ElasticsearchProcessInstanceStateStore(
            IElasticClient elasticClient,
            ILogger<ElasticsearchProcessInstanceStateStore> logger)
        {
            _elasticClient = elasticClient ?? throw new ArgumentNullException(nameof(elasticClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jsonSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                Formatting = Formatting.None
            };

            EnsureIndexExistsAsync().GetAwaiter().GetResult();
            EnsureStateIndexExistsAsync().GetAwaiter().GetResult();
        }

        private async Task EnsureIndexExistsAsync()
        {
            var exists = await _elasticClient.Indices.ExistsAsync(IndexName);
            if (exists.Exists) return;

            var create = await _elasticClient.Indices.CreateAsync(IndexName, c => c
                .Settings(s => s
                    .NumberOfShards(1)
                    .NumberOfReplicas(1)
                    .RefreshInterval("1s"))
                .Map(m => m
                    .Properties(ps => ps
                        .Keyword(k => k.Name("instanceId").IgnoreAbove(256))
                        .Keyword(k => k.Name("deploymentId").IgnoreAbove(256))
                        .Keyword(k => k.Name("status").IgnoreAbove(64))
                        .Object<object>(o => o.Name("payload").Dynamic())
                        .Date(d => d.Name("updatedAt"))
                    )
                )
            );

            if (!create.IsValid)
            {
                _logger.LogError("Failed to create index '{Index}': {Error}", IndexName, create.DebugInformation);
                throw new ElasticsearchClientException($"Cannot create index {IndexName}: {create.DebugInformation}");
            }
        }

        private async Task EnsureStateIndexExistsAsync()
        {
            var exists = await _elasticClient.Indices.ExistsAsync(StateIndexName);
            if (exists.Exists) return;

            var create = await _elasticClient.Indices.CreateAsync(StateIndexName, c => c
                .Settings(s => s
                    .NumberOfShards(1)
                    .NumberOfReplicas(1)
                    .RefreshInterval("1s"))
                .Map(m => m
                    .Properties(ps => ps
                        .Keyword(k => k.Name("key").IgnoreAbove(256))
                        .Number(n => n.Name("version").Type(NumberType.Long))
                        .Object<object>(o => o.Name("state").Dynamic())
                        .Date(d => d.Name("updatedAt"))
                    )
                )
            );

            if (!create.IsValid)
            {
                _logger.LogError("Failed to create index '{Index}': {Error}", StateIndexName, create.DebugInformation);
                throw new ElasticsearchClientException($"Cannot create index {StateIndexName}: {create.DebugInformation}");
            }
        }
    }

    // The rest of the implementation broken into partial classes for clarity
    public partial class ElasticsearchProcessInstanceStateStore
    {
        Task<ProcessInstanceState?> IProcessInstanceStateStore.GetAsync(string processInstanceId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(processInstanceId))
                throw new ArgumentException("ProcessInstanceId must be provided", nameof(processInstanceId));

            return GetAsyncInternal(processInstanceId, cancellationToken);
        }

        private async Task<ProcessInstanceState?> GetAsyncInternal(string processInstanceId, CancellationToken cancellationToken)
        {
            var response = await _elasticClient.GetAsync<Dictionary<string, object>>(processInstanceId, g => g
                .Index(IndexName),
                cancellationToken);

            if (!response.IsValid)
            {
                if (response.ApiCall.HttpStatusCode == 404) return null;
                _logger.LogError("Error retrieving state for '{Id}': {Error}", processInstanceId, response.DebugInformation);
                throw new ElasticsearchClientException($"GetAsync failed: {response.DebugInformation}");
            }

            if (!response.Found || !response.Source.TryGetValue("payload", out var raw)) return null;

            try
            {
                var json = raw.ToString()!;
                return JsonConvert.DeserializeObject<ProcessInstanceState>(json, _jsonSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deserialization error for process instance '{Id}'", processInstanceId);
                throw;
            }
        }

        Task IProcessInstanceStateStore.SaveAsync(ProcessInstanceState state, CancellationToken cancellationToken)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (string.IsNullOrWhiteSpace(state.InstanceId))
                throw new ArgumentException("InstanceId must be set on the state", nameof(state));

            return SaveAsyncInternal(state, cancellationToken);
        }

        private async Task SaveAsyncInternal(ProcessInstanceState state, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var document = new
            {
                instanceId = state.InstanceId,
                deploymentId = state.DeploymentKey,
                status = state.Status.ToString(),
                payload = JsonConvert.SerializeObject(state, _jsonSettings),
                updatedAt = now
            };

            var response = await _elasticClient.IndexAsync(document, i => i
                .Index(IndexName)
                .Id(state.InstanceId)
                .Refresh(Refresh.True),
                cancellationToken);

            if (!response.IsValid)
            {
                _logger.LogError("Error saving state for '{Id}': {Error}", state.InstanceId, response.DebugInformation);
                throw new ElasticsearchClientException($"SaveAsync failed: {response.DebugInformation}");
            }
        }

        Task IProcessInstanceStateStore.DeleteAsync(string processInstanceId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(processInstanceId))
                throw new ArgumentException("ProcessInstanceId must be provided", nameof(processInstanceId));

            return DeleteAsyncInternal(processInstanceId, cancellationToken);
        }

        private async Task DeleteAsyncInternal(string processInstanceId, CancellationToken cancellationToken)
        {
            var response = await _elasticClient.DeleteAsync(new DeleteRequest(IndexName, processInstanceId), cancellationToken);
            if (!response.IsValid && response.ApiCall.HttpStatusCode != 404)
            {
                _logger.LogError("Error deleting state for '{Id}': {Error}", processInstanceId, response.DebugInformation);
                throw new ElasticsearchClientException($"DeleteAsync failed: {response.DebugInformation}");
            }
        }

        public async Task<bool> ExistsAsync(string processInstanceId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(processInstanceId))
                throw new ArgumentException("ProcessInstanceId must be provided", nameof(processInstanceId));

            var response = await _elasticClient.DocumentExistsAsync(new DocumentExistsRequest(IndexName, processInstanceId), cancellationToken);
            if (!response.IsValid)
            {
                _logger.LogError("Error checking existence for '{Id}': {Error}", processInstanceId, response.DebugInformation);
                throw new ElasticsearchClientException($"ExistsAsync failed: {response.DebugInformation}");
            }
            return response.Exists;
        }
    }

    // Query methods
    public partial class ElasticsearchProcessInstanceStateStore
    {
        Task<IReadOnlyList<ProcessInstanceState>> IProcessInstanceStateStore.QueryByInstanceIdAsync(string instanceId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                throw new ArgumentException("instanceId is required", nameof(instanceId));

            return QueryByInstanceIdAsyncInternal(instanceId, cancellationToken);
        }

        private async Task<IReadOnlyList<ProcessInstanceState>> QueryByInstanceIdAsyncInternal(string instanceId, CancellationToken cancellationToken)
        {
            var resp = await _elasticClient.SearchAsync<Dictionary<string, object>>(s => s
                .Index(IndexName)
                .Size(1000)
                .Query(q => q
                    .Term(t => t.Field("instanceId").Value(instanceId))),
                cancellationToken);

            if (!resp.IsValid)
                throw new ElasticsearchClientException($"QueryByInstanceIdAsync failed: {resp.DebugInformation}");

            var list = new List<ProcessInstanceState>();
            foreach (var hit in resp.Hits)
            {
                if (hit.Source.TryGetValue("payload", out var raw))
                {
                    var state = JsonConvert.DeserializeObject<ProcessInstanceState>(
                        raw.ToString()!,
                        _jsonSettings);
                    if (state != null)
                        list.Add(state);
                }
            }
            return list;
        }

        Task<IReadOnlyList<ProcessInstanceState>> IProcessInstanceStateStore.QueryByDeploymentIdAsync(string deploymentId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(deploymentId))
                throw new ArgumentException("deploymentId is required", nameof(deploymentId));

            return QueryByDeploymentIdAsyncInternal(deploymentId, cancellationToken);
        }

        private async Task<IReadOnlyList<ProcessInstanceState>> QueryByDeploymentIdAsyncInternal(string deploymentId, CancellationToken cancellationToken)
        {
            var resp = await _elasticClient.SearchAsync<Dictionary<string, object>>(s => s
                .Index(IndexName)
                .Size(1000)
                .Query(q => q
                    .Term(t => t.Field("deploymentId").Value(deploymentId))),
                cancellationToken);

            if (!resp.IsValid)
                throw new ElasticsearchClientException($"QueryByDeploymentIdAsync failed: {resp.DebugInformation}");

            var list = new List<ProcessInstanceState>();
            foreach (var hit in resp.Hits)
            {
                if (hit.Source.TryGetValue("payload", out var raw))
                {
                    var state = JsonConvert.DeserializeObject<ProcessInstanceState>(
                        raw.ToString()!,
                        _jsonSettings);
                    if (state != null)
                        list.Add(state);
                }
            }
            return list;
        }

        Task<IReadOnlyList<ProcessInstanceState>> IProcessInstanceStateStore.QueryByStatusAsync(string status, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(status))
                throw new ArgumentException("status is required", nameof(status));

            return QueryByStatusAsyncInternal(status, cancellationToken);
        }

        private async Task<IReadOnlyList<ProcessInstanceState>> QueryByStatusAsyncInternal(string status, CancellationToken cancellationToken)
        {
            var resp = await _elasticClient.SearchAsync<Dictionary<string, object>>(s => s
                .Index(IndexName)
                .Size(1000)
                .Query(q => q
                    .Term(t => t.Field("status").Value(status))),
                cancellationToken);

            if (!resp.IsValid)
                throw new ElasticsearchClientException($"QueryByStatusAsync failed: {resp.DebugInformation}");

            var list = new List<ProcessInstanceState>();
            foreach (var hit in resp.Hits)
            {
                if (hit.Source.TryGetValue("payload", out var raw))
                {
                    var state = JsonConvert.DeserializeObject<ProcessInstanceState>(
                        raw.ToString()!,
                        _jsonSettings);
                    if (state != null)
                        list.Add(state);
                }
            }
            return list;
        }
    }

    // Generic state handling
    public partial class ElasticsearchProcessInstanceStateStore
    {
        public async Task<T?> GetStateAsync<T>(string processInstanceId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(processInstanceId))
                throw new ArgumentException("processInstanceId is required", nameof(processInstanceId));

            var response = await _elasticClient.GetAsync<Dictionary<string, object>>(processInstanceId, g => g
                .Index(StateIndexName),
                cancellationToken);

            if (!response.IsValid)
            {
                if (response.ApiCall.HttpStatusCode == 404) return default;
                _logger.LogError("Error retrieving state for '{Id}': {Error}", processInstanceId, response.DebugInformation);
                throw new ElasticsearchClientException($"GetStateAsync failed: {response.DebugInformation}");
            }

            if (!response.Found || !response.Source.TryGetValue("state", out var raw)) return default;

            try
            {
                var json = raw.ToString()!;
                return JsonConvert.DeserializeObject<T>(json, _jsonSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deserialization error for state '{Id}'", processInstanceId);
                throw;
            }
        }

        public async Task<object?> GetStateAsync(string processInstanceId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(processInstanceId))
                throw new ArgumentException("processInstanceId is required", nameof(processInstanceId));

            var response = await _elasticClient.GetAsync<Dictionary<string, object>>(processInstanceId, g => g
                .Index(StateIndexName),
                cancellationToken);

            if (!response.IsValid)
            {
                if (response.ApiCall.HttpStatusCode == 404) return null;
                _logger.LogError("Error retrieving state for '{Id}': {Error}", processInstanceId, response.DebugInformation);
                throw new ElasticsearchClientException($"GetStateAsync failed: {response.DebugInformation}");
            }

            if (!response.Found || !response.Source.TryGetValue("state", out var raw)) return null;

            try
            {
                var json = raw.ToString()!;
                return JsonConvert.DeserializeObject(json, _jsonSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deserialization error for state '{Id}'", processInstanceId);
                throw;
            }
        }

        public async Task<StateWithVersion<T>> GetStateWithVersionAsync<T>(string processInstanceId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(processInstanceId))
                throw new ArgumentException("processInstanceId is required", nameof(processInstanceId));

            var response = await _elasticClient.GetAsync<Dictionary<string, object>>(processInstanceId, g => g
                .Index(StateIndexName),
                cancellationToken);

            if (!response.IsValid)
            {
                if (response.ApiCall.HttpStatusCode == 404)
                    return new StateWithVersion<T> { State = default, Version = 0 };
                
                _logger.LogError("Error retrieving state for '{Id}': {Error}", processInstanceId, response.DebugInformation);
                throw new ElasticsearchClientException($"GetStateWithVersionAsync failed: {response.DebugInformation}");
            }

            if (!response.Found)
                return new StateWithVersion<T> { State = default, Version = 0 };

            long version = 0;
            T? state = default;

            if (response.Source.TryGetValue("version", out var versionObj))
            {
                version = Convert.ToInt64(versionObj);
            }

            if (response.Source.TryGetValue("state", out var stateObj))
            {
                try
                {
                    var json = stateObj.ToString()!;
                    state = JsonConvert.DeserializeObject<T>(json, _jsonSettings);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Deserialization error for state '{Id}'", processInstanceId);
                    throw;
                }
            }

            return new StateWithVersion<T> { State = state, Version = version };
        }

        public async Task SaveStateAsync(string processInstanceId, object state, long? expectedVersion = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(processInstanceId))
                throw new ArgumentException("processInstanceId is required", nameof(processInstanceId));
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            long nextVersion = 1;

            // If expectedVersion is provided, we need to check the current version
            if (expectedVersion.HasValue)
            {
                var response = await _elasticClient.GetAsync<Dictionary<string, object>>(processInstanceId, g => g
                    .Index(StateIndexName),
                    cancellationToken);

                if (response.IsValid && response.Found && response.Source.TryGetValue("version", out var versionObj))
                {
                    long currentVersion = Convert.ToInt64(versionObj);
                    if (currentVersion != expectedVersion.Value)
                    {
                        throw new InvalidOperationException($"Version mismatch. Expected: {expectedVersion.Value}, Actual: {currentVersion}");
                    }
                    nextVersion = currentVersion + 1;
                }
            }

            var now = DateTime.UtcNow;
            var document = new
            {
                key = processInstanceId,
                version = nextVersion,
                state = JsonConvert.SerializeObject(state, _jsonSettings),
                updatedAt = now
            };

            var indexResponse = await _elasticClient.IndexAsync(document, i => i
                .Index(StateIndexName)
                .Id(processInstanceId)
                .Refresh(Refresh.True),
                cancellationToken);

            if (!indexResponse.IsValid)
            {
                _logger.LogError("Error saving state for '{Id}': {Error}", processInstanceId, indexResponse.DebugInformation);
                throw new ElasticsearchClientException($"SaveStateAsync failed: {indexResponse.DebugInformation}");
            }
        }

        public async Task DeleteStateAsync(string processInstanceId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(processInstanceId))
                throw new ArgumentException("processInstanceId is required", nameof(processInstanceId));

            var response = await _elasticClient.DeleteAsync(
                new DeleteRequest(StateIndexName, processInstanceId),
                cancellationToken);

            if (!response.IsValid && response.ApiCall.HttpStatusCode != 404)
            {
                _logger.LogError("Error deleting state for '{Id}': {Error}", processInstanceId, response.DebugInformation);
                throw new ElasticsearchClientException($"DeleteStateAsync failed: {response.DebugInformation}");
            }
        }

        public async Task<IReadOnlyList<T>> FindStatesByPatternAsync<T>(string pattern, Func<T, bool>? predicate = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentException("pattern is required", nameof(pattern));

            // Convert pattern to Elasticsearch wildcard format
            var wildcardPattern = pattern.Replace('*', '?');
            
            var searchResponse = await _elasticClient.SearchAsync<Dictionary<string, object>>(s => s
                .Index(StateIndexName)
                .Size(1000)
                .Query(q => q
                    .Wildcard(w => w
                        .Field("key")
                        .Value(wildcardPattern))),
                cancellationToken);

            if (!searchResponse.IsValid)
                throw new ElasticsearchClientException($"FindStatesByPatternAsync failed: {searchResponse.DebugInformation}");

            var results = new List<T>();
            foreach (var hit in searchResponse.Hits)
            {
                if (hit.Source.TryGetValue("state", out var stateObj))
                {
                    try
                    {
                        var json = stateObj.ToString()!;
                        var deserializedState = JsonConvert.DeserializeObject<T>(json, _jsonSettings);
                        
                        if (deserializedState != null && (predicate == null || predicate(deserializedState)))
                        {
                            results.Add(deserializedState);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Deserialization error for state with key pattern '{Pattern}'", pattern);
                        // Continue with other results instead of failing
                    }
                }
            }

            return results;
        }

        public Task SaveAsync(ProcessInstanceState state, CancellationToken cancellationToken = default)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (string.IsNullOrWhiteSpace(state.InstanceId))
                throw new ArgumentException("InstanceId must be set on the state", nameof(state));

            var now = DateTime.UtcNow;
            var document = new
            {
                instanceId = state.InstanceId,
                deploymentId = state.DeploymentKey,
                status = state.Status.ToString(),
                payload = JsonConvert.SerializeObject(state, _jsonSettings),
                updatedAt = now
            };

            return _elasticClient.IndexAsync(document, i => i
                .Index(IndexName)
                .Id(state.InstanceId)
                .Refresh(Refresh.True),
                cancellationToken);
        }
    }
}
