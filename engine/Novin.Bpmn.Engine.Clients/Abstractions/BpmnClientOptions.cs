namespace Novin.Bpmn.Engine.Clients.Abstractions;

/// <summary>
/// Configuration options for BPMN engine client
/// </summary>
public class BpmnClientOptions
{
    private string _clientId = string.Empty;
    private string _engineBaseUrl = string.Empty;

    /// <summary>
    /// Unique identifier for this client
    /// </summary>
    public string ClientId
    {
        get => _clientId;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("ClientId cannot be null or empty", nameof(ClientId));
            _clientId = value;
        }
    }

    /// <summary>
    /// Base URL of the BPMN engine server
    /// </summary>
    public string EngineBaseUrl
    {
        get => _engineBaseUrl;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("EngineBaseUrl cannot be null or empty", nameof(EngineBaseUrl));

            // Ensure URL is properly formatted
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                throw new ArgumentException("EngineBaseUrl must be a valid absolute URL", nameof(EngineBaseUrl));

            _engineBaseUrl = uri.ToString().TrimEnd('/');
        }
    }

    /// <summary>
    /// Timeout for SignalR connections (in seconds)
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of concurrent work items to process
    /// </summary>
    public int MaxConcurrentWorkItems { get; set; } = 10;

    /// <summary>
    /// Whether to enable detailed logging
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Retry policy for failed work items
    /// </summary>
    public RetryPolicy RetryPolicy { get; set; } = new();

    /// <summary>
    /// Health check configuration
    /// </summary>
    public HealthCheckOptions HealthCheck { get; set; } = new();
}

/// <summary>
/// Retry policy configuration
/// </summary>
public class RetryPolicy
{
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay between retries (in seconds)
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 1;

    /// <summary>
    /// Maximum delay between retries (in seconds)
    /// </summary>
    public int MaxDelaySeconds { get; set; } = 60;

    /// <summary>
    /// Backoff multiplier for delay calculation
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;
}

/// <summary>
/// Health check configuration
/// </summary>
public class HealthCheckOptions
{
    /// <summary>
    /// Whether health checks are enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Health check interval (in seconds)
    /// </summary>
    public int IntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Timeout for health check operations (in seconds)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Configuration for a service worker
/// </summary>
public class ServiceWorkerConfig
{
    /// <summary>
    /// Unique identifier for the service worker
    /// </summary>
    public string WorkerId { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the service worker
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this worker does
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Types of work this worker can handle
    /// </summary>
    public List<string> SupportedWorkTypes { get; set; } = new();

    /// <summary>
    /// Maximum number of concurrent tasks for this worker
    /// </summary>
    public int MaxConcurrentTasks { get; set; } = 5;

    /// <summary>
    /// Whether this worker is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Handler type that implements the work processing logic
    /// </summary>
    public Type? HandlerType { get; set; }
}