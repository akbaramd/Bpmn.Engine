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

        const int maxRetries = 5;
        int retryCount = 0;
        bool success = false;

        while (!success && retryCount <= maxRetries)
        {
            try
            {
                // First get the current document to get sequence numbers for optimistic concurrency
                var getResponse = await _es.GetAsync<Dictionary<string, object>>(
                    new DocumentPath<Dictionary<string, object>>(state.InstanceId),
                    d => d.Index(IndexName)
                );
                
                // Prepare document to index
                long newVersion = 1; // Default for new documents
                long? ifSeqNo = null;
                long? ifPrimaryTerm = null;
                long currentVersion = 0;
                
                if (getResponse.IsValid && getResponse.Found)
                {
                    // Document exists, extract current version and always increment it
                    if (getResponse.Source.TryGetValue("version", out var v))
                    {
                        currentVersion = Convert.ToInt64(v);
                        // Always use DB version + 1, regardless of what expectedVersion is
                        newVersion = currentVersion + 1;
                        
                        // Check expectedVersion only for optimistic concurrency validation
                        // and only when explicitly provided (not null)
                        if (expectedVersion.HasValue && currentVersion != expectedVersion.Value)
                        {
                            // Instead of failing with an exception, we'll just log a warning 
                            // and proceed with state merging. This ensures we don't lose updates
                            // even when concurrent updates are happening despite our locks
                            _log.LogWarning("Version mismatch for '{InstanceId}'. Expected {ExpectedVersion}, got {CurrentVersion}. " +
                                           "Proceeding with merged state.",
                                state.InstanceId, expectedVersion.Value, currentVersion);
                        }
                    }
                    
                    // Always set sequence number and primary term for optimistic concurrency
                    ifSeqNo = getResponse.SequenceNumber;
                    ifPrimaryTerm = getResponse.PrimaryTerm;
                    
                    // Always merge with current state if it exists
                    // This ensures we don't lose updates from parallel flows
                    var currentJson = getResponse.Source.TryGetValue("state", out var stateJson) 
                        ? stateJson?.ToString() 
                        : null;
                        
                    if (currentJson != null)
                    {
                        var currentState = JsonSerializer.Deserialize<ProcessInstanceState>(currentJson, _json);
                        if (currentState != null)
                        {
                            _log.LogDebug("Merging state for {InstanceId} (retry: {RetryCount}, version: {CurrentVersion} -> {NewVersion})", 
                                state.InstanceId, retryCount, currentVersion, newVersion);
                            
                            MergeStates(state, currentState);
                        }
                    }
                }
                else if (expectedVersion.HasValue && expectedVersion.Value > 0)
                {
                    // Document doesn't exist but expectedVersion is specified (and not zero)
                    throw new InvalidOperationException(
                        $"Document '{state.InstanceId}' doesn't exist but version {expectedVersion.Value} was expected");
                }
                
                // Always update the lastUpdatedAt timestamp to ensure proper ordering
                state.LastUpdatedAt = DateTime.UtcNow;
                
                var doc = new
                {
                    instanceId   = state.InstanceId,
                    deploymentId = state.DeploymentId,
                    deploymentKey= state.DeploymentKey,
                    status       = state.Status.ToString(),
                    version      = newVersion,
                    state        = JsonSerializer.Serialize(state, _json),
                    updatedAt    = DateTime.UtcNow
                };
                
                // Build the index request
                var indexRequest = new IndexRequest<object>(IndexName, state.InstanceId)
                {
                    Document = doc,
                    Refresh = Refresh.True
                };
                
                // Apply optimistic concurrency control ONLY if document exists
                if (ifSeqNo.HasValue && ifPrimaryTerm.HasValue)
                {
                    // Both IfSequenceNumber and IfPrimaryTerm must be provided together
                    indexRequest.IfSequenceNumber = ifSeqNo;
                    indexRequest.IfPrimaryTerm = ifPrimaryTerm;
                    
                    _log.LogDebug("Using optimistic concurrency for {InstanceId}: Version={Version}, SeqNo={SeqNo}, PrimaryTerm={PrimaryTerm}", 
                        state.InstanceId, newVersion, ifSeqNo, ifPrimaryTerm);
                }
                else
                {
                    _log.LogDebug("Document {InstanceId} doesn't exist yet, creating new with version 1", state.InstanceId);
                }

                var resp = await _es.IndexAsync(indexRequest, ct);

                if (!resp.IsValid)
                {
                    if (resp.ServerError?.Status == 409 || 
                        (resp.ServerError?.Error?.Type?.Contains("conflict") == true))
                    {
                        retryCount++;
                        
                        if (retryCount > maxRetries)
                        {
                            _log.LogError("Failed to update state for {InstanceId} after {MaxRetries} retries due to concurrency conflicts: {Error}", 
                                state.InstanceId, maxRetries, resp.DebugInformation);
                            throw new InvalidOperationException(
                                $"Failed to update state for '{state.InstanceId}' after {maxRetries} retries due to concurrency conflicts");
                        }1
                        
                        _log.LogWarning("Concurrency conflict detected for {InstanceId}. Retrying {RetryCount}/{MaxRetries}. Error: {Error}", 
                            state.InstanceId, retryCount, maxRetries, resp.DebugInformation);
                            
                        // Wait before retry with exponential backoff
                        int delay = Math.Min(100 * (int)Math.Pow(2, retryCount), 5000);
                        await Task.Delay(delay, ct);
                    }
                    else
                    {
                        // Not a concurrency issue, throw regular error
                        ThrowIfInvalid(resp, $"index {state.InstanceId}");
                    }
                }
                else
                {
                    success = true;
                    _log.LogInformation("Successfully updated state for {InstanceId} with version {Version}", 
                        state.InstanceId, doc.version);
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Version mismatch"))
            {
                retryCount++;
                
                if (retryCount > maxRetries)
                {
                    _log.LogError(ex, "Failed to update state for {InstanceId} after {MaxRetries} retries", 
                        state.InstanceId, maxRetries);
                    throw;
                }
                
                _log.LogWarning("Version mismatch for {InstanceId}. Retrying {RetryCount}/{MaxRetries}", 
                    state.InstanceId, retryCount, maxRetries);
                    
                // Wait before retry with exponential backoff
                int delay = Math.Min(100 * (int)Math.Pow(2, retryCount), 5000);
                await Task.Delay(delay, ct);
            }
        }
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

            return new StateWithVersion<ProcessInstanceState>(obj, ver);
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

        const int maxRetries = 3;
        int retryCount = 0;
        bool success = false;

        while (!success && retryCount <= maxRetries)
        {
            try
            {
                // Get the document to get sequence numbers for optimistic concurrency
                var getResponse = await _es.GetAsync<Dictionary<string, object>>(
                    new DocumentPath<Dictionary<string, object>>(instanceId),
                    d => d.Index(IndexName)
                );
                
                // Document doesn't exist
                if (!getResponse.IsValid || !getResponse.Found)
                {
                    return false;
                }
                
                // Check version if specified
                if (expectedVersion.HasValue)
                {
                    if (getResponse.Source.TryGetValue("version", out var v))
                    {
                        long currentVersion = Convert.ToInt64(v);
                        if (currentVersion != expectedVersion.Value)
                        {
                            throw new InvalidOperationException(
                                $"Version mismatch for '{instanceId}'. Expected {expectedVersion.Value}, got {currentVersion}");
                        }
                    }
                }
                
                // Use sequence numbers for optimistic concurrency
                var deleteRequest = new DeleteRequest(IndexName, instanceId)
                {
                    IfSequenceNumber = getResponse.SequenceNumber,
                    IfPrimaryTerm = getResponse.PrimaryTerm,
                    Refresh = Refresh.True
                };
                
                var resp = await _es.DeleteAsync(deleteRequest, ct);
                
                if (!resp.IsValid)
                {
                    if (resp.ServerError?.Status == 409 || 
                        (resp.ServerError?.Error?.Type?.Contains("conflict") == true))
                    {
                        retryCount++;
                        
                        if (retryCount > maxRetries)
                        {
                            _log.LogError("Failed to delete {InstanceId} after {MaxRetries} retries due to concurrency conflicts", 
                                instanceId, maxRetries);
                            throw new InvalidOperationException(
                                $"Failed to delete '{instanceId}' after {maxRetries} retries due to concurrency conflicts");
                        }
                        
                        _log.LogWarning("Concurrency conflict when deleting {InstanceId}. Retrying {RetryCount}/{MaxRetries}", 
                            instanceId, retryCount, maxRetries);
                            
                        // Wait before retry with exponential backoff
                        await Task.Delay(100 * (int)Math.Pow(2, retryCount), ct);
                    }
                    else if (resp.ApiCall.HttpStatusCode is 404)
                    {
                        return false;
                    }
                    else
                    {
                        ThrowIfInvalid(resp, $"delete {instanceId}");
                    }
                }
                else
                {
                    success = true;
                    return true;
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Version mismatch"))
            {
                retryCount++;
                
                if (retryCount > maxRetries)
                {
                    _log.LogError(ex, "Failed to delete {InstanceId} after {MaxRetries} retries", 
                        instanceId, maxRetries);
                    throw;
                }
                
                _log.LogWarning("Version mismatch when deleting {InstanceId}. Retrying {RetryCount}/{MaxRetries}", 
                    instanceId, retryCount, maxRetries);
                    
                // Wait before retry with exponential backoff
                await Task.Delay(100 * (int)Math.Pow(2, retryCount), ct);
            }
        }
        
        return false;
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

    /// <summary>
    /// Merges two process instance states to handle concurrent updates
    /// </summary>
    private void MergeStates(ProcessInstanceState target, ProcessInstanceState source)
    {
        // Don't merge if the source is a previous version of the target
        if (source.LastUpdatedAt < target.LastUpdatedAt)
        {
            _log.LogDebug("Skipping merge for {InstanceId} as source state is older than target state", target.InstanceId);
            return;
        }
        
        // Merge variables (target vars take precedence for conflicting keys)
        foreach (var kvp in source.Variables)
        {
            if (!target.Variables.ContainsKey(kvp.Key))
            {
                target.Variables[kvp.Key] = kvp.Value;
                _log.LogDebug("Merged variable {Key} from source state", kvp.Key);
            }
        }

        // Merge history by unique event IDs
        var targetEventIds = target.History.Select(e => e.EventId).ToHashSet();
        foreach (var evt in source.History)
        {
            if (!targetEventIds.Contains(evt.EventId))
            {
                target.History.Add(evt);
                _log.LogDebug("Merged history event {EventId} from source state", evt.EventId);
            }
        }

        // Use the new MergeExecutions method to handle executions more safely
        // This handles all the execution merging logic cleanly
        target.MergeExecutions(source.Executions.Values);
        _log.LogDebug("Merged {Count} executions from source state", source.Executions.Count);
        
        // Merge subscriptions by ID
        var targetSubIds = target.Subscriptions.Select(s => s.SubscriptionId).ToHashSet();
        foreach (var subscription in source.Subscriptions)
        {
            if (!targetSubIds.Contains(subscription.SubscriptionId))
            {
                target.Subscriptions.Add(subscription);
                _log.LogDebug("Merged subscription {SubscriptionId} from source state", subscription.SubscriptionId);
            }
        }
        
        // Merge jobs by ID
        var targetJobIds = target.Jobs.Select(j => j.JobId).ToHashSet();
        foreach (var job in source.Jobs)
        {
            if (!targetJobIds.Contains(job.JobId))
            {
                target.Jobs.Add(job);
                _log.LogDebug("Merged job {JobId} from source state", job.JobId);
            }
        }
        
        // Merge incidents by ID
        var targetIncidentIds = target.Incidents.Select(i => i.IncidentId).ToHashSet();
        foreach (var incident in source.Incidents)
        {
            if (!targetIncidentIds.Contains(incident.IncidentId))
            {
                target.Incidents.Add(incident);
                _log.LogDebug("Merged incident {IncidentId} from source state", incident.IncidentId);
            }
        }
        
        // Update process status if source is more "terminal" than target
        if (IsMoreTerminal(source.Status, target.Status))
        {
            _log.LogDebug("Updating process status from {OldStatus} to {NewStatus} due to merge", 
                target.Status, source.Status);
                
            target.Status = source.Status;
            
            // Copy completion info if applicable
            if (source.CompletedAt.HasValue)
            {
                target.CompletedAt = source.CompletedAt;
            }
        }
    }
    
    /// <summary>
    /// Determines if newStatus represents a more terminal state than oldStatus
    /// </summary>
    private bool IsMoreTerminal(ProcessInstanceStatus newStatus, ProcessInstanceStatus oldStatus)
    {
        // Terminal states (in order of precedence): Completed > Failed > Terminated/Cancelled
        // Active states (in order): Active > Waiting > Suspended
        
        // If both statuses are the same, there's no change
        if (newStatus == oldStatus) return false;
        
        // If old status is already terminal, don't change it
        if (oldStatus == ProcessInstanceStatus.Completed ||
            oldStatus == ProcessInstanceStatus.Failed)
        {
            return false;
        }
        
        // Always allow transition to Completed from any non-terminal state
        if (newStatus == ProcessInstanceStatus.Completed)
        {
            return true;
        }
        
        // Allow transition to Failed from any non-terminal and non-Completed state
        if (newStatus == ProcessInstanceStatus.Failed && 
            oldStatus != ProcessInstanceStatus.Completed)
        {
            return true;
        }
        
        // Allow transition to Terminated/Cancelled from any non-terminal, non-Completed, non-Failed state
        if ((newStatus == ProcessInstanceStatus.Terminated || newStatus == ProcessInstanceStatus.Cancelled) &&
            oldStatus != ProcessInstanceStatus.Completed && 
            oldStatus != ProcessInstanceStatus.Failed)
        {
            return true;
        }
        
        // For non-terminal states, prefer more "active" states
        if (!IsTerminalStatus(oldStatus) && !IsTerminalStatus(newStatus))
        {
            // Active > Waiting > Suspended
            if (newStatus == ProcessInstanceStatus.Active)
            {
                return true;
            }
            
            if (newStatus == ProcessInstanceStatus.Waiting && 
                oldStatus == ProcessInstanceStatus.Suspended)
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Merges execution details (variables and events) without changing the execution status
    /// </summary>
    private void MergeExecutionDetails(ElementExecution target, ElementExecution source)
    {
        // Merge variables
        foreach (var varKvp in source.LocalVariables)
        {
            if (!target.LocalVariables.ContainsKey(varKvp.Key))
            {
                target.LocalVariables[varKvp.Key] = varKvp.Value;
            }
        }
        
        // Merge events by ID
        var targetEventIds = target.Events.Select(e => e.EventId).ToHashSet();
        foreach (var evt in source.Events)
        {
            if (!targetEventIds.Contains(evt.EventId))
            {
                target.Events.Add(evt);
            }
        }
    }
    
    /// <summary>
    /// Check if execution status is terminal (completed, failed, terminated)
    /// </summary>
    private bool IsTerminalStatus(ExecutionStatus status)
    {
        return status == ExecutionStatus.Completed || 
               status == ExecutionStatus.Failed ||
               status == ExecutionStatus.Terminated;
    }
    
    /// <summary>
    /// Check if process status is terminal
    /// </summary>
    private bool IsTerminalStatus(ProcessInstanceStatus status)
    {
        return status == ProcessInstanceStatus.Completed ||
               status == ProcessInstanceStatus.Failed ||
               status == ProcessInstanceStatus.Terminated ||
               status == ProcessInstanceStatus.Cancelled;
    }

    #endregion
}
