using System.Text.Json;
using Elasticsearch.Net;
using Microsoft.Extensions.Logging;
using Nest;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Json;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.EventSourcing.Events;

public sealed class ElasticsearchProcessInstanceStateStore : IProcessInstanceStateStore
{
    private const string IndexName = "bpmn-process-instance-generic-states";

    private readonly IElasticClient _es;
    private readonly ILogger<ElasticsearchProcessInstanceStateStore> _log;
    private readonly JsonSerializerOptions _json;

    public ElasticsearchProcessInstanceStateStore(
        IElasticClient es,
        ILogger<ElasticsearchProcessInstanceStateStore> log)
    {
        _es  = es  ?? throw new ArgumentNullException(nameof(es));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        _json = new JsonSerializerOptions 
        {
           
            IncludeFields         = false,
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            Converters =
            {
                new BpmnElementTypeJsonConverter(),
                new ProcessInstanceStatusJsonConverter(),
                new ExecutionStatusJsonConverter(),
                new ObjectDictionaryJsonConverter()
            },    
         
        };

        EnsureIndexAsync().GetAwaiter().GetResult();
    }

    #region ------------- Public API -------------

    public async Task UpsertAsync(ProcessInstanceState state,
                                  long? expectedVersion = null,
                                  CancellationToken ct  = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(state.InstanceId);

        if (expectedVersion.HasValue)
            await EnsureVersionAsync(state.InstanceId, expectedVersion.Value, ct);

        var doc = new
        {
            instanceId   = state.InstanceId,
            deploymentId = state.DeploymentId,
            deploymentKey= state.DeploymentKey,
            status       = state.Status.ToString(),
            version      = expectedVersion.GetValueOrDefault() + 1,
            state        = JsonSerializer.Serialize(state, _json),
            updatedAt    = DateTime.UtcNow
        };

        var resp = await _es.IndexAsync(doc, i => i.Index(IndexName)
                                                   .Id(state.InstanceId)
                                                   .Refresh(Refresh.True), ct);

        ThrowIfInvalid(resp, $"index {state.InstanceId}");
    }

    public async Task<StateWithVersion<ProcessInstanceState>?> GetAsync(
        string instanceId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var resp = await _es.GetAsync<Dictionary<string, object>>(instanceId,
                      g => g.Index(IndexName), ct);

        if (!resp.IsValid || !resp.Found) return null;

        var json = resp.Source["state"]?.ToString();
        
        // Debug logging
        try
        {
            var obj = json is null ? null
                                  : JsonSerializer.Deserialize<ProcessInstanceState>(json, _json);
            
            if (obj != null)
            {
                _log.LogDebug("Successfully deserialized ProcessInstanceState for {InstanceId}. History count: {Count}", 
                              instanceId, obj.History?.Count ?? 0);
            }
            else
            {
                _log.LogWarning("Deserialized ProcessInstanceState is null for {InstanceId}", instanceId);
            }

            long ver = resp.Source.TryGetValue("version", out var v) ? Convert.ToInt64(v) : 0;

            return new StateWithVersion<ProcessInstanceState> { State = obj, Version = ver };
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error deserializing ProcessInstanceState for {InstanceId}. JSON: {JSON}", 
                         instanceId, json?.Substring(0, Math.Min(json.Length, 500)));
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string instanceId,
                                        long? expectedVersion = null,
                                        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        if (expectedVersion.HasValue)
            await EnsureVersionAsync(instanceId, expectedVersion.Value, ct);

        var resp = await _es.DeleteAsync(new DeleteRequest(IndexName, instanceId), ct);

        if (resp.ApiCall.HttpStatusCode is 404) return false;
        ThrowIfInvalid(resp, $"delete {instanceId}");
        return true;
    }

    public async Task<bool> ExistsAsync(string instanceId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        var resp = await _es.DocumentExistsAsync(new DocumentExistsRequest(IndexName, instanceId), ct);
        ThrowIfInvalid(resp, $"exists {instanceId}");
        return resp.Exists;
    }

    public async Task<IReadOnlyList<ProcessInstanceState>> QueryAsync(
        InstanceQuery query, CancellationToken ct = default)
    {
        if (query is null) throw new ArgumentNullException(nameof(query));

        var must = new List<Func<QueryContainerDescriptor<Dictionary<string, object>>, QueryContainer>>();

        if (query.InstanceId   != null) must.Add(q => q.Term("instanceId", query.InstanceId));
        if (query.DeploymentId != null) must.Add(q => q.Term("deploymentId", query.DeploymentId));
        if (query.Status       != null) must.Add(q => q.Term("status",       query.Status));
        if (query.Pattern      != null) must.Add(q => q.Wildcard(w =>
                                         w.Field("instanceId").Value(query.Pattern)));

        var resp = await _es.SearchAsync<Dictionary<string, object>>(s => s
                     .Index(IndexName)
                     .Size(query.Size)
                     .Query(q => q.Bool(b => b.Must(must))), ct);

        ThrowIfInvalid(resp, "search");

        var list = new List<ProcessInstanceState>();

        foreach (var hit in resp.Hits)
        {
            if (hit.Source.TryGetValue("state", out var raw))
            {
                var obj = JsonSerializer.Deserialize<ProcessInstanceState>(
                              raw.ToString()!, _json);
                if (obj != null) list.Add(obj);
            }
        }
        return list;
    }

    #endregion

    #region ------------- Helpers -------------

    private async Task EnsureVersionAsync(string id, long expected, CancellationToken ct)
    {
        var current = await GetAsync(id, ct);
        if (current?.Version != expected)
            throw new InvalidOperationException(
                $"Version mismatch for '{id}'. Expected {expected}, got {current?.Version}");
    }

    private static void ThrowIfInvalid(IResponse resp, string op)
    {
        if (resp.IsValid) return;
        throw new ElasticsearchClientException($"Elasticsearch {op} failed: {resp.DebugInformation}");
    }

    private async Task EnsureIndexAsync()
    {
        var exists = await _es.Indices.ExistsAsync(IndexName);
        if (exists.Exists) return;

        var create = await _es.Indices.CreateAsync(IndexName, c => c
            .Settings(s => s.NumberOfShards(1).NumberOfReplicas(1))
            .Map(m => m
                .Properties(ps => ps
                    .Keyword(k => k.Name("instanceId"))
                    .Keyword(k => k.Name("deploymentId"))
                    .Keyword(k => k.Name("deploymentKey"))
                    .Keyword(k => k.Name("status"))
                    .Number(n => n.Name("version").Type(NumberType.Long))
                    .Date(d => d.Name("updatedAt"))
                )));

        ThrowIfInvalid(create, $"create-index {IndexName}");
    }

    #endregion
}
