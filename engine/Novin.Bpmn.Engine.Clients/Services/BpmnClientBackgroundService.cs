using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Novin.Bpmn.Engine.Clients.Abstractions;

namespace Novin.Bpmn.Engine.Clients.Services;

/// <summary>
/// Background service that automatically connects to BPMN engine on startup
/// and handles work requests from the engine
/// </summary>
public class BpmnClientBackgroundService : BackgroundService
{
    private readonly BpmnClientOptions _options;
    private readonly IClientConnectionManager _connectionManager;
    private readonly IServiceWorkerRegistry _workerRegistry;
    private readonly ILogger<BpmnClientBackgroundService> _logger;

    public BpmnClientBackgroundService(
        BpmnClientOptions options,
        IClientConnectionManager connectionManager,
        IServiceWorkerRegistry workerRegistry,
        ILogger<BpmnClientBackgroundService> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _workerRegistry = workerRegistry ?? throw new ArgumentNullException(nameof(workerRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BPMN Client Background Service starting up");

        try
        {
            // Discover and log all available workers
            DiscoverAndLogWorkers();

            // Auto-connect to the BPMN engine
            await AutoConnectToEngineAsync(stoppingToken);

            // Keep the service running and monitor connection
            await MonitorConnectionAsync(stoppingToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("BPMN Client Background Service stopping - cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in BPMN Client Background Service");
            throw;
        }
    }

    private void DiscoverAndLogWorkers()
    {
        try
        {
            var allWorkers = _workerRegistry.GetAllWorkers().ToList();

            if (!allWorkers.Any())
            {
                _logger.LogWarning("No service workers found. Client will not be able to handle work requests. This may be due to PostConfigure not having run yet.");
                _logger.LogInformation("Worker registry will be checked again during connection.");
                return;
            }

            _logger.LogInformation("Discovered {WorkerCount} service workers:", allWorkers.Count);

            foreach (var worker in allWorkers)
            {
                _logger.LogInformation(
                    "Worker '{WorkerId}' ({Name}) - Status: {Status}, Work Types: {WorkTypes}",
                    worker.WorkerId,
                    worker.Name,
                    worker.Enabled ? "Enabled" : "Disabled",
                    string.Join(", ", worker.SupportedWorkTypes));

                if (!worker.Enabled)
                {
                    _logger.LogWarning("Worker '{WorkerId}' is disabled and will not handle requests", worker.WorkerId);
                }
            }

            var totalWorkTypes = allWorkers.SelectMany(w => w.SupportedWorkTypes).Distinct().ToList();
            _logger.LogInformation("Client supports {WorkTypeCount} unique work types: {WorkTypes}",
                totalWorkTypes.Count, string.Join(", ", totalWorkTypes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering workers. Worker registry may not be properly initialized.");
        }
    }

    private void VerifyWorkersAfterConnection()
    {
        try
        {
            var allWorkers = _workerRegistry.GetAllWorkers().ToList();

            if (allWorkers.Any())
            {
                var totalWorkTypes = allWorkers.SelectMany(w => w.SupportedWorkTypes).Distinct().ToList();
                _logger.LogInformation("Worker verification after connection: {WorkerCount} workers supporting {WorkTypeCount} work types",
                    allWorkers.Count, totalWorkTypes.Count);
            }
            else
            {
                _logger.LogWarning("No workers found even after connection. The client may not function properly.");
                _logger.LogWarning("Ensure that AddServiceWorker<T>() calls are made in Program.cs before app.Build()");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during worker verification after connection");
        }
    }

    private async Task AutoConnectToEngineAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Attempting to auto-connect to BPMN engine at {EngineUrl} as client '{ClientId}'",
            _options.EngineBaseUrl, _options.ClientId);

        var maxRetries = _options.ConnectionTimeoutSeconds > 0 ? 5 : 1;
        var retryDelay = TimeSpan.FromSeconds(5);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _connectionManager.RegisterWithEngineAsync(stoppingToken);
                _logger.LogInformation("Successfully connected to BPMN engine on attempt {Attempt}/{MaxAttempts}",
                    attempt, maxRetries);

                // Verify workers after successful connection
                VerifyWorkersAfterConnection();
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, "Connection attempt {Attempt}/{MaxAttempts} failed, retrying in {Delay}s",
                    attempt, maxRetries, retryDelay.TotalSeconds);
                await Task.Delay(retryDelay, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to BPMN engine after {MaxAttempts} attempts", maxRetries);
                throw;
            }
        }
    }

    private async Task MonitorConnectionAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Connection monitoring started. Client is ready to receive work requests.");

        var consecutiveFailures = 0;
        const int maxConsecutiveFailures = 10;
        const int baseDelaySeconds = 30;
        const int maxDelaySeconds = 300; // 5 minutes

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check connection status periodically
                var status = _connectionManager.GetConnectionStatus();

                if (!status.IsConnected)
                {
                    consecutiveFailures++;
                    var delaySeconds = Math.Min(baseDelaySeconds * consecutiveFailures, maxDelaySeconds);

                    _logger.LogWarning("Connection lost (failure #{ConsecutiveFailures}). Attempting to reconnect in {Delay}s...",
                        consecutiveFailures, delaySeconds);

                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);

                    try
                    {
                        await _connectionManager.RegisterWithEngineAsync(stoppingToken);
                        _logger.LogInformation("Successfully reconnected to BPMN engine after {ConsecutiveFailures} failures", consecutiveFailures);
                        consecutiveFailures = 0; // Reset on success
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to reconnect to BPMN engine (attempt #{ConsecutiveFailures})", consecutiveFailures);

                        if (consecutiveFailures >= maxConsecutiveFailures)
                        {
                            _logger.LogCritical("Maximum consecutive connection failures ({MaxFailures}) reached. Client may be misconfigured.", maxConsecutiveFailures);
                            // Continue monitoring but with longer delays
                            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                        }
                    }
                }
                else
                {
                    // Reset failure count when connected
                    if (consecutiveFailures > 0)
                    {
                        consecutiveFailures = 0;
                        _logger.LogInformation("Connection restored, resetting failure counter");
                    }

                    // Log periodic status if detailed logging is enabled
                    if (_options.EnableDetailedLogging)
                    {
                        var uptime = status.LastConnectedAt.HasValue
                            ? DateTime.UtcNow - status.LastConnectedAt.Value
                            : TimeSpan.Zero;

                        _logger.LogDebug("Connection status: Connected for {Uptime}, Last attempt: {LastAttempt}",
                            uptime, status.LastConnectionAttempt);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Check every 30 seconds
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in connection monitoring loop");
                consecutiveFailures++;
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(60 * consecutiveFailures, 300)), stoppingToken);
            }
        }

        _logger.LogInformation("Connection monitoring stopped");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BPMN Client Background Service stopping");

        try
        {
            // Disconnect from the engine gracefully
            if (_connectionManager.IsConnected())
            {
                await _connectionManager.UnregisterFromEngineAsync(cancellationToken);
                _logger.LogInformation("Successfully disconnected from BPMN engine");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during graceful shutdown");
        }

        await base.StopAsync(cancellationToken);
        _logger.LogInformation("BPMN Client Background Service stopped");
    }
}