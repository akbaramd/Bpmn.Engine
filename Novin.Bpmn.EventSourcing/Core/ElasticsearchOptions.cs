using System;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// Configuration options for Elasticsearch
/// </summary>
public class ElasticsearchOptions
{
    /// <summary>
    /// Elasticsearch server URL
    /// </summary>
    public string Url { get; set; } = "http://localhost:9200";

    /// <summary>
    /// Username for Elasticsearch authentication
    /// </summary>
    public string Username { get; set; } = "elastic";

    /// <summary>
    /// Password for Elasticsearch authentication
    /// </summary>
    public string Password { get; set; } = "changeme";

    /// <summary>
    /// Index prefix for BPMN events
    /// </summary>
    public string IndexPrefix { get; set; } = "bpmn-events-";

    /// <summary>
    /// Number of shards per index
    /// </summary>
    public int NumberOfShards { get; set; } = 1;

    /// <summary>
    /// Number of replicas per shard
    /// </summary>
    public int NumberOfReplicas { get; set; } = 0;

    /// <summary>
    /// Maximum number of events to return in a single query
    /// </summary>
    public int MaxResultWindow { get; set; } = 10000;

    /// <summary>
    /// Scroll timeout for event subscriptions
    /// </summary>
    public TimeSpan ScrollTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to enable SSL/TLS
    /// </summary>
    public bool EnableSsl { get; set; } = false;

    /// <summary>
    /// Whether to verify SSL certificates
    /// </summary>
    public bool VerifySsl { get; set; } = true;

    /// <summary>
    /// Connection timeout
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of retries for failed requests
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Retry timeout
    /// </summary>
    public TimeSpan RetryTimeout { get; set; } = TimeSpan.FromSeconds(5);
} 