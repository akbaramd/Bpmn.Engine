using System;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class ElasticIndexBootstrapperHostedService : BackgroundService
{
    private readonly ElasticsearchClient _es;
    private readonly ElasticOptions _opt;
    private readonly ILogger<ElasticIndexBootstrapperHostedService> _logger;

    public ElasticIndexBootstrapperHostedService(
        ElasticsearchClient es,
        IOptions<ElasticOptions> opt,
        ILogger<ElasticIndexBootstrapperHostedService> logger)
    {
        _es = es ?? throw new ArgumentNullException(nameof(es));
        _opt = opt?.Value ?? throw new ArgumentNullException(nameof(opt));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureIndexAsync(stoppingToken);
    }

    private async Task EnsureIndexAsync(CancellationToken ct)
    {
        var index = _opt.Index;

        await WaitForElasticsearchAsync(ct);

        var exists = await _es.Indices.ExistsAsync(index, ct);
        if (exists.IsValidResponse && exists.Exists)
        {
            _logger.LogInformation("[ES] Index exists: {Index}", index);
            return;
        }

        _logger.LogInformation("[ES] Creating index: {Index}", index);

        var create = await _es.Indices.CreateAsync(index, c => c
            .Settings(s => s
                .NumberOfShards(_opt.Shards <= 0 ? 1 : _opt.Shards)
                .NumberOfReplicas(_opt.Replicas < 0 ? 0 : _opt.Replicas)
                .RefreshInterval(new Elastic.Clients.Elasticsearch.Duration(_opt.RefreshInterval ?? "1s"))
            )
            .Mappings(m => m
                .Dynamic(DynamicMapping.True)
                .Properties(new Properties
                {
                    // Identity / routing
                    ["outboxId"]      = new KeywordProperty(),
                    ["partitionKey"]  = new KeywordProperty(),
                    ["correlationId"] = new KeywordProperty(),
                    ["aggregateId"]   = new KeywordProperty(),

                    // Message
                    ["messageName"]   = new KeywordProperty(),
                    ["messageType"]   = new KeywordProperty(),

                    // Claim/lease
                    ["status"]         = new KeywordProperty(), // pending|processing|processed|failed
                    ["lockId"]         = new KeywordProperty(),
                    ["lockedUntilUtc"] = new DateProperty(),
                    ["nextAttemptOnUtc"] = new DateProperty(),
                    ["attempts"]       = new IntegerNumberProperty(),

                    // Timing / errors
                    ["occurredAtUtc"]  = new DateProperty(),
                    ["lastError"]      = new TextProperty(),

                    // Payload (dynamic object)
                    ["payload"] = new ObjectProperty
                    {
                        Dynamic = DynamicMapping.True
                    }
                })
            )
        , ct);

        if (!create.IsValidResponse)
            throw new InvalidOperationException($"[ES] Failed to create index '{index}': {create.DebugInformation}");

        _logger.LogInformation("[ES] Index created: {Index}", index);
    }

    private async Task WaitForElasticsearchAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromMilliseconds(250);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ping = await _es.PingAsync(ct);
                if (ping.IsValidResponse)
                {
                    _logger.LogInformation("[ES] Connected.");
                    return;
                }

                _logger.LogWarning("[ES] Ping invalid: {Info}", ping.DebugInformation);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ES] Not reachable yet.");
            }

            await Task.Delay(delay, ct);

            if (delay < TimeSpan.FromSeconds(5))
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 5000));
        }
    }
}

