using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;
using Novin.Bpmn.Engine.Infrastructure.Outbox.Redis;



// IMPORTANT: use Elastic.Transport.HttpMethod (NOT System.Net.Http.HttpMethod)
using HttpMethod = Elastic.Transport.HttpMethod;

public sealed class ElasticOutboxClaimer : IOutboxBatchClaimer
{
    private readonly ElasticsearchClient _es;
    private readonly string _index;

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ElasticOutboxClaimer(ElasticsearchClient es, string indexName = "bpmn-outbox")
    {
        _es = es ?? throw new ArgumentNullException(nameof(es));
        _index = indexName ?? throw new ArgumentNullException(nameof(indexName));
    }
public async Task<IReadOnlyList<OutboxDispatchItem>> ClaimAsync(
    int batchSize,
    TimeSpan lease,
    CancellationToken ct)
{
    var now = DateTime.UtcNow;
    var lockedUntil = now.Add(lease);

    // 1) Search eligible docs (ask ES to return seq_no & primary_term)
    var searchJson = BuildClaimSearchJson(batchSize, now);
    var searchPath = new EndpointPath(
        HttpMethod.POST,
        $"/{_index}/_search?seq_no_primary_term=true"
    );

    var searchResp = await _es.Transport.RequestAsync<BytesResponse>(
        searchPath,
        PostData.String(searchJson),
        configureActivity: null,
        localConfiguration: null,
        cancellationToken: ct);

    if (!searchResp.ApiCallDetails.HasSuccessfulStatusCode)
        throw new InvalidOperationException(
            $"[ES] Search failed. Status={searchResp.ApiCallDetails.HttpStatusCode}");

    var hits = ParseHits(searchResp.Body);
    if (hits.Count == 0)
        return Array.Empty<OutboxDispatchItem>();

    // 2) Bulk claim (optimistic concurrency: if_seq_no + if_primary_term)
    var ndjson = BuildBulkClaimNdjson(hits, lockedUntil);
    var bulkPath = new EndpointPath(HttpMethod.POST, "/_bulk");

    var bulkResp = await _es.Transport.RequestAsync<BytesResponse>(
        bulkPath,
        PostData.String(ndjson),
        configureActivity: null,
        localConfiguration: null,
        cancellationToken: ct);

    if (!bulkResp.ApiCallDetails.HasSuccessfulStatusCode)
        throw new InvalidOperationException(
            $"[ES] Bulk claim failed. Status={bulkResp.ApiCallDetails.HttpStatusCode}");

    // 3) Keep only successfully claimed docs
    var successMask = ParseBulkSuccessMask(bulkResp.Body, hits.Count);

    var claimed = new List<OutboxDispatchItem>(hits.Count);

    for (int i = 0; i < hits.Count; i++)
    {
        if (!successMask[i]) continue;

        var hit = hits[i];

        // ES _id should be Guid (we store outboxId as document id)
        if (!TryParseGuid(hit.Id, out var outboxId))
            continue;

        var d = hit.Doc;

        claimed.Add(new OutboxDispatchItem(
            OutboxId: outboxId,
            PartitionKey: string.IsNullOrWhiteSpace(d.PartitionKey) ? "global" : d.PartitionKey!,
            MessageType: d.MessageType ?? "",
            Payload: d.Payload?.ToJsonString() ?? "{}",   // keep your system compatible (string payload)
            MessageName: d.MessageName ?? "",
            OccurredAtUtc: d.OccurredAtUtc == default ? now : d.OccurredAtUtc,
            Attempts: d.Attempts
        ));
    }

    return claimed;
}

private static bool TryParseGuid(string? s, out Guid g)
{
    g = default;
    if (string.IsNullOrWhiteSpace(s)) return false;

    // supports both "N" and normal Guid formats
    return Guid.TryParseExact(s, "N", out g) || Guid.TryParse(s, out g);
}


    // ------------------------
    // Search JSON
    // ------------------------
    private static string BuildClaimSearchJson(int size, DateTime nowUtc)
    {
        var now = nowUtc.ToString("O");

        // eligible:
        // - pending
        // - failed AND (nextAttempt missing OR nextAttempt <= now)
        // - processing AND lockedUntilUtc <= now (reclaim)
        return $$"""
{
  "size": {{size}},
  "sort": [{ "occurredAtUtc": { "order": "asc" } }],
  "query": {
    "bool": {
      "should": [
        { "term": { "status": "pending" } },
        {
          "bool": {
            "must": [
              { "term": { "status": "failed" } },
              {
                "bool": {
                  "should": [
                    { "bool": { "must_not": { "exists": { "field": "nextAttemptOnUtc" } } } },
                    { "range": { "nextAttemptOnUtc": { "lte": "{{now}}" } } }
                  ],
                  "minimum_should_match": 1
                }
              }
            ]
          }
        },
        {
          "bool": {
            "must": [
              { "term": { "status": "processing" } },
              { "range": { "lockedUntilUtc": { "lte": "{{now}}" } } }
            ]
          }
        }
      ],
      "minimum_should_match": 1
    }
  }
}
""";
    }

    private sealed record Hit(string Id, long SeqNo, long PrimaryTerm, OutboxDoc Doc);

    private static List<Hit> ParseHits(byte[] body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (!root.TryGetProperty("hits", out var hitsObj)) return new();
        if (!hitsObj.TryGetProperty("hits", out var hitsArr)) return new();

        var list = new List<Hit>();

        foreach (var h in hitsArr.EnumerateArray())
        {
            var id = h.GetProperty("_id").GetString();
            if (string.IsNullOrWhiteSpace(id)) continue;

            var seqNo = h.TryGetProperty("_seq_no", out var s) ? s.GetInt64() : 0;
            var pTerm = h.TryGetProperty("_primary_term", out var p) ? p.GetInt64() : 0;

            OutboxDoc source = new();
            if (h.TryGetProperty("_source", out var src))
                source = JsonSerializer.Deserialize<OutboxDoc>(src.GetRawText(), JsonOpt) ?? new OutboxDoc();

            list.Add(new Hit(id, seqNo, pTerm, source));
        }

        return list;
    }

    // ------------------------
    // Bulk NDJSON
    // ------------------------
    private string BuildBulkClaimNdjson(IReadOnlyList<Hit> hits,  DateTime lockedUntilUtc)
    {
        var sb = new StringBuilder(hits.Count * 256);

        foreach (var h in hits)
        {
            // action line
            sb.AppendLine(JsonSerializer.Serialize(new
            {
                update = new
                {
                    _index = _index,
                    _id = h.Id,
                    if_seq_no = h.SeqNo,
                    if_primary_term = h.PrimaryTerm
                }
            }, JsonOpt));

            // source line
            sb.AppendLine(JsonSerializer.Serialize(new
            {
                doc = new
                {
                    status = "processing",
                    lockedUntilUtc = lockedUntilUtc,
                    attempts = h.Doc.Attempts + 1,
                    nextAttemptOnUtc = (DateTime?)null
                }
            }, JsonOpt));
        }

        return sb.ToString();
    }

    private static bool[] ParseBulkSuccessMask(byte[] body, int expected)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var mask = new bool[expected];
        if (!root.TryGetProperty("items", out var items)) return mask;

        var i = 0;
        foreach (var item in items.EnumerateArray())
        {
            if (i >= expected) break;

            if (item.TryGetProperty("update", out var upd))
            {
                var status = upd.TryGetProperty("status", out var st) ? st.GetInt32() : 0;
                var hasError = upd.TryGetProperty("error", out _);

                mask[i] = (status >= 200 && status < 300) && !hasError;
            }

            i++;
        }

        return mask;
    }
}

public sealed record ClaimedDoc(string Id, OutboxDoc Doc);

public sealed class OutboxDoc
{
    public string Status { get; set; } = "pending"; // pending|processing|processed|failed
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LockedUntilUtc { get; set; }
    public string? LockId { get; set; }

    public DateTime? NextAttemptOnUtc { get; set; }
    public int Attempts { get; set; }

    public string? MessageName { get; set; }
    public string? MessageType { get; set; }
    public string? PartitionKey { get; set; }

    public Guid? CorrelationId { get; set; }
    public Guid? AggregateId { get; set; }

    public JsonNode? Payload { get; set; }
    public string? LastError { get; set; }
}
