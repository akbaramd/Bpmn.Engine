using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;

using HttpMethod = Elastic.Transport.HttpMethod;

namespace Novin.Bpmn.Engine.Infrastructure.Outbox.Elastices;

public sealed class ElasticOutboxStateStore : IOutboxStateStore
{
    private readonly ElasticsearchClient _es;
    private readonly string _index;

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ElasticOutboxStateStore(ElasticsearchClient es, string indexName = "bpmn-outbox")
    {
        _es = es ?? throw new ArgumentNullException(nameof(es));
        _index = indexName ?? throw new ArgumentNullException(nameof(indexName));
    }

    public Task MarkDispatchedAsync(IReadOnlyList<Guid> ids, DateTime dispatchedAtUtc, CancellationToken ct)
        => BulkUpdateSameDocAsync(ids, doc: new
        {
            status = "dispatched",
            dispatchedAtUtc
        }, ct);

    public Task MarkProcessedAsync(Guid id, DateTime processedAtUtc, CancellationToken ct)
        => UpdateDocAsync(id, doc: new
        {
            status = "processed",
            processedAtUtc,
            lastError = (string?)null
        }, ct);

    public Task MarkFailedAsync(Guid id, string error, DateTime? nextAttemptUtc, CancellationToken ct)
        => UpdateDocAsync(id, doc: new
        {
            status = "failed",
            lastError = error,
            nextAttemptOnUtc = nextAttemptUtc,
            lockId = (string?)null,
            lockedUntilUtc = (DateTime?)null
        }, ct);

    // ✅ Bulk processed
    public Task MarkProcessedBulkAsync(IReadOnlyList<Guid> ids, DateTime processedAtUtc, CancellationToken ct)
        => BulkUpdateSameDocAsync(ids, doc: new
        {
            status = "processed",
            processedAtUtc,
            lastError = (string?)null
        }, ct);

    // ✅ Bulk failed (per-item error)
    public async Task MarkFailedBulkAsync(
        IReadOnlyList<(Guid Id, string Error)> failed,
        DateTime failedAtUtc,
        DateTime? nextAttemptUtc,
        CancellationToken ct)
    {
        if (failed == null || failed.Count == 0) return;

        var sb = new StringBuilder(failed.Count * 220);

        for (var i = 0; i < failed.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var (id, error) = failed[i];

            sb.Append("{\"update\":{\"_index\":");
            sb.Append(JsonSerializer.Serialize(_index, JsonOpt));
            sb.Append(",\"_id\":");
            sb.Append(JsonSerializer.Serialize(id.ToString("N"), JsonOpt));
            sb.Append("}}");
            sb.Append('\n');

            var body = new
            {
                doc = new
                {
                    status = "failed",
                    failedAtUtc,
                    lastError = error,
                    nextAttemptOnUtc = nextAttemptUtc,
                    lockId = (string?)null,
                    lockedUntilUtc = (DateTime?)null
                }
            };

            sb.Append(JsonSerializer.Serialize(body, JsonOpt));
            sb.Append('\n');
        }

        var path = new EndpointPath(HttpMethod.POST, "/_bulk?refresh=false");

        var resp = await _es.Transport.RequestAsync<BytesResponse>(
            path,
            PostData.String(sb.ToString()),
            configureActivity: null,
            localConfiguration: null,
            cancellationToken: ct);

        if (!resp.ApiCallDetails.HasSuccessfulStatusCode)
            throw new InvalidOperationException($"[ES] BulkUpdateFailed failed. Status={resp.ApiCallDetails.HttpStatusCode}");
    }

    private async Task UpdateDocAsync(Guid id, object doc, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(new { doc }, JsonOpt);
        var path = new EndpointPath(HttpMethod.POST, $"/{_index}/_update/{id:N}?refresh=false");

        var resp = await _es.Transport.RequestAsync<BytesResponse>(
            path,
            PostData.String(json),
            configureActivity: null,
            localConfiguration: null,
            cancellationToken: ct);

        if (!resp.ApiCallDetails.HasSuccessfulStatusCode)
            throw new InvalidOperationException($"[ES] Update({_index}/{id:N}) failed. Status={resp.ApiCallDetails.HttpStatusCode}");
    }

    private async Task BulkUpdateSameDocAsync(IReadOnlyList<Guid> ids, object doc, CancellationToken ct)
    {
        if (ids == null || ids.Count == 0) return;

        var sb = new StringBuilder(ids.Count * 170);
        var docLine = JsonSerializer.Serialize(new { doc }, JsonOpt);

        for (var i = 0; i < ids.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var id = ids[i];

            sb.Append("{\"update\":{\"_index\":");
            sb.Append(JsonSerializer.Serialize(_index, JsonOpt));
            sb.Append(",\"_id\":");
            sb.Append(JsonSerializer.Serialize(id.ToString("N"), JsonOpt));
            sb.Append("}}");
            sb.Append('\n');

            sb.Append(docLine);
            sb.Append('\n');
        }

        var path = new EndpointPath(HttpMethod.POST, "/_bulk?refresh=false");

        var resp = await _es.Transport.RequestAsync<BytesResponse>(
            path,
            PostData.String(sb.ToString()),
            configureActivity: null,
            localConfiguration: null,
            cancellationToken: ct);

        if (!resp.ApiCallDetails.HasSuccessfulStatusCode)
            throw new InvalidOperationException($"[ES] BulkUpdate failed. Status={resp.ApiCallDetails.HttpStatusCode}");
    }

public Task MarkProcessedBulkAsync(List<Guid> processed, DateTime now, CancellationToken ct)
{
    // re-use fast-path bulk update
    return BulkUpdateSameDocAsync(processed, doc: new
    {
        status = "processed",
        processedAtUtc = now,
        lastError = (string?)null
    }, ct);
}

public Task MarkFailedBulkAsync(
    List<(Guid Id, string Error)> failed,
    DateTime now,
    object nextAttemptUtc,
    CancellationToken ct)
{
    // Convert object -> DateTime?
    DateTime? next = nextAttemptUtc switch
    {
        null => null,
        DateTime dt => dt,
        DateTimeOffset dto => dto.UtcDateTime,
        string s when DateTime.TryParse(s, out var parsed) => parsed,
        _ => null
    };

    // Re-use the per-item bulk-failed implementation (fast)
    return MarkFailedBulkAsync(
        failed: failed,
        failedAtUtc: now,
        nextAttemptUtc: next,
        ct: ct);
}
}
