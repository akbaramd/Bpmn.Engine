using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

namespace Novin.Bpmn.Engine.Infrastructure.Outbox.Elastices;

public interface IElasticOutboxWriter
{
    Task WritePendingAsync(Guid id, OutboxDoc doc, CancellationToken ct);
    Task WritePendingBulkAsync(IReadOnlyList<(string Id, OutboxDoc Doc)> docs, CancellationToken ct);
}

public sealed class ElasticOutboxWriter : IElasticOutboxWriter
{
    private readonly ElasticsearchClient _es;
    private readonly string _index;

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ElasticOutboxWriter(ElasticsearchClient es, string indexName = "bpmn-outbox")
    {
        _es = es ?? throw new ArgumentNullException(nameof(es));
        _index = indexName ?? throw new ArgumentNullException(nameof(indexName));
    }

    public async Task WritePendingAsync(Guid id, OutboxDoc doc, CancellationToken ct)
    {
        if (doc is null) throw new ArgumentNullException(nameof(doc));

        Normalize(doc);

        var json = JsonSerializer.Serialize(doc, JsonOpt);

        var path = new EndpointPath(
            Elastic.Transport.HttpMethod.PUT,
            $"/{_index}/_doc/{id:N}?refresh=false");

        var resp = await _es.Transport.RequestAsync<BytesResponse>(
            path,
            PostData.String(json),
            configureActivity: null,
            localConfiguration: null,
            cancellationToken: ct);

        EnsureOk(resp, "[ES] WritePending failed");
    }

    /// <summary>
    /// Bulk upsert documents into Elasticsearch using _bulk NDJSON.
    /// Uses "index" op (idempotent by Id): re-sending overwrites same id.
    /// </summary>
    public async Task WritePendingBulkAsync(IReadOnlyList<(string Id, OutboxDoc Doc)> docs, CancellationToken ct)
    {
        if (docs is null) throw new ArgumentNullException(nameof(docs));
        if (docs.Count == 0) return;

        // Build NDJSON: action line + source line per doc
        // Example:
        // {"index":{"_index":"bpmn-outbox","_id":"..."}}
        // {"status":"pending",...}
        var sb = new StringBuilder(capacity: docs.Count * 256);

        for (var i = 0; i < docs.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var (id, doc) = docs[i];
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Bulk doc Id cannot be null/empty.", nameof(docs));
            if (doc is null)
                throw new ArgumentException("Bulk doc Doc cannot be null.", nameof(docs));

            Normalize(doc);

            // action/meta
            sb.Append("{\"index\":{\"_index\":");
            sb.Append(JsonSerializer.Serialize(_index, JsonOpt)); // safe JSON string
            sb.Append(",\"_id\":");
            sb.Append(JsonSerializer.Serialize(id, JsonOpt));     // safe JSON string
            sb.Append("}}");
            sb.Append('\n');

            // source
            sb.Append(JsonSerializer.Serialize(doc, JsonOpt));
            sb.Append('\n');
        }

        var path = new EndpointPath(Elastic.Transport.HttpMethod.POST, "/_bulk?refresh=false");

        var resp = await _es.Transport.RequestAsync<BytesResponse>(
            path,
            PostData.String(sb.ToString()),
            configureActivity: null,
            localConfiguration: null,
            cancellationToken: ct);

        EnsureOk(resp, "[ES] WritePendingBulk failed");

        // Optional (recommended): detect item-level errors in bulk response.
        // BytesResponse doesn't parse; if you want strict check, switch to a typed response
        // or parse resp.Body as JSON and check "errors": true.
    }

    private static void Normalize(OutboxDoc doc)
    {
        doc.Status = "pending";
        if (doc.OccurredAtUtc == default)
            doc.OccurredAtUtc = DateTime.UtcNow;
    }

    private static void EnsureOk(BytesResponse resp, string message)
    {
        if (!resp.ApiCallDetails.HasSuccessfulStatusCode)
            throw new InvalidOperationException(
                $"{message}. Status={resp.ApiCallDetails.HttpStatusCode}");
    }
}
