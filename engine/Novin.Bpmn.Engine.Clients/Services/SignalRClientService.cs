using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Novin.Bpmn.Engine.Clients.Abstractions;
using Novin.Bpmn.Engine.Domain.Communication;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Engine.Application.Services;
using System.Text.Json;

namespace Novin.Bpmn.Engine.Clients.Services;

/// <summary>
/// SignalR client service for communicating with the BPMN engine
/// </summary>
public class SignalRClientService : ISignalRClientService, IAsyncDisposable
{
    private readonly BpmnClientOptions _options;
    private readonly IServiceWorkerRegistry _workerRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SignalRClientService> _logger;

    private HubConnection? _hubConnection;
    private bool _isConnected;

    public SignalRClientService(
        BpmnClientOptions options,
        IServiceWorkerRegistry workerRegistry,
        IServiceProvider serviceProvider,
        ILogger<SignalRClientService> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _workerRegistry = workerRegistry ?? throw new ArgumentNullException(nameof(workerRegistry));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts the SignalR connection to the engine
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task StartConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_hubConnection != null)
        {
            _logger.LogWarning("SignalR connection already exists");
            return;
        }

        try
        {
            // Validate configuration
            if (string.IsNullOrWhiteSpace(_options.EngineBaseUrl))
            {
                throw new InvalidOperationException("EngineBaseUrl is not configured");
            }

            if (string.IsNullOrWhiteSpace(_options.ClientId))
            {
                throw new InvalidOperationException("ClientId is not configured");
            }

            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{_options.EngineBaseUrl.TrimEnd('/')}/bpmn/clientHub")
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.FromSeconds(0),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30)
                })
                .Build();

            // Register event handlers
            RegisterEventHandlers();

            // Register service task execution handler
            _hubConnection.On<WorkerTaskRequest>("ExecuteServiceTask", async (request) =>
            {
                try
                {
                    await HandleServiceTaskExecutionAsync(request);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error in service task execution for request {ExecutionId}", request?.ExecutionId);
                }
            });

            // Register service task completion acknowledgment handler
            // Receives dictionary directly from SignalR - no JSON conversion
            _hubConnection.On<Guid, Dictionary<string, string>>("ServiceTaskCompletedAck", async (workerId, result) =>
            {
                try
                {
                    // Result dictionary is passed directly - use exactly what was sent
                    await HandleServiceTaskCompletedAckAsync(workerId, result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error in service task completion acknowledgment for worker {WorkerId}", workerId);
                }
            });

            _logger.LogInformation("Starting SignalR connection to {EngineUrl} as client {ClientId}",
                _options.EngineBaseUrl, _options.ClientId);
            await _hubConnection.StartAsync(cancellationToken);
            _isConnected = true;

            // Register the client with the engine
            await RegisterClientAsync(cancellationToken);

            _logger.LogInformation("SignalR connection established and client registered successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SignalR connection. Client will not be able to process service tasks.");
            await CleanupConnectionAsync();
            throw;
        }
    }

    /// <summary>
    /// Stops the SignalR connection to the engine
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task StopConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_hubConnection == null)
        {
            _logger.LogWarning("SignalR connection does not exist");
            return;
        }

        try
        {
            _isConnected = false;

            // Try to unregister the client gracefully
            try
            {
                await UnregisterClientAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error unregistering client during shutdown");
            }

            // Stop the connection
            await _hubConnection.StopAsync(cancellationToken);
            _logger.LogInformation("SignalR connection stopped gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping SignalR connection");
        }
        finally
        {
            // Always cleanup resources
            await CleanupConnectionAsync();
        }
    }

    /// <summary>
    /// Sends a work completion notification to the engine
    /// </summary>
    /// <param name="workItemId">The work item ID</param>
    /// <param name="result">The work result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task SendWorkCompletedAsync(Guid workItemId, object result, CancellationToken cancellationToken = default)
    {
        if (_hubConnection == null || !_isConnected)
            throw new InvalidOperationException("SignalR connection is not established");

        await _hubConnection.InvokeAsync("WorkCompleted", workItemId, result, cancellationToken);
    }

    /// <summary>
    /// Sends a work failure notification to the engine
    /// </summary>
    /// <param name="workItemId">The work item ID</param>
    /// <param name="error">The error details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task SendWorkFailedAsync(Guid workItemId, string error, CancellationToken cancellationToken = default)
    {
        if (_hubConnection == null || !_isConnected)
            throw new InvalidOperationException("SignalR connection is not established");

        await _hubConnection.InvokeAsync("WorkFailed", workItemId, error, cancellationToken);
    }

    /// <summary>
    /// Sends a heartbeat to the engine
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task SendHeartbeatAsync(CancellationToken cancellationToken = default)
    {
        if (_hubConnection == null || !_isConnected)
            throw new InvalidOperationException("SignalR connection is not established");

        await _hubConnection.InvokeAsync("Heartbeat", _options.ClientId, cancellationToken);
    }

    /// <summary>
    /// Sends service task completion notification to the engine
    /// Converts dictionary values to strings and sends as Dictionary<string, string>
    /// </summary>
    /// <param name="executionId">The service task execution ID</param>
    /// <param name="result">The execution result dictionary - values converted to strings</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task SendServiceTaskCompletedAsync(
        Guid executionId,
        Dictionary<string, string>? result = null,
        CancellationToken cancellationToken = default)
    {
        if (_hubConnection == null)
            throw new InvalidOperationException("SignalR connection is not initialized");

        // Wait for connection to be active with timeout
        const int maxRetries = 3;
        const int retryDelayMs = 1000;

        for (int i = 0; i < maxRetries; i++)
        {
            if (_isConnected && _hubConnection.State == HubConnectionState.Connected)
            {
                // Send as Dictionary<string, string> - SignalR will handle serialization
                await _hubConnection.InvokeAsync("ServiceTaskCompleted", executionId, result, cancellationToken);
                return;
            }

            if (i < maxRetries - 1)
            {
                _logger.LogWarning("SignalR connection not ready, retrying in {Delay}ms (attempt {Attempt}/{Max})",
                    retryDelayMs, i + 1, maxRetries);
                await Task.Delay(retryDelayMs, cancellationToken);
            }
        }

        throw new InvalidOperationException($"SignalR connection is not established after {maxRetries} attempts");
    }

    /// <summary>
    /// Converts a value to string representation
    /// </summary>
    private static string ConvertValueToString(object? value)
    {
        if (value == null)
            return string.Empty;

        if (value is string str)
            return str;

        // Use JSON serialization for complex types
        return Newtonsoft.Json.JsonConvert.SerializeObject(value);
    }

    /// <summary>
    /// Sends service task failure notification to the engine
    /// </summary>
    /// <param name="executionId">The service task execution ID</param>
    /// <param name="error">The error message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task SendServiceTaskFailedAsync(
        Guid executionId,
        string error,
        CancellationToken cancellationToken = default)
    {
        const int maxRetries = 3;
        const int retryDelayMs = 1000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                if (_hubConnection == null)
                    throw new InvalidOperationException("SignalR connection is not initialized");

                if (_isConnected && _hubConnection.State == HubConnectionState.Connected)
                {
                    await _hubConnection.InvokeAsync("ServiceTaskFailed", executionId, error, cancellationToken);
                    return;
                }
            }
            catch (Exception ex) when (i < maxRetries - 1)
            {
                _logger.LogWarning(ex, "Failed to send service task failure notification (attempt {Attempt}), retrying...", i + 1);
            }

            if (i < maxRetries - 1)
            {
                await Task.Delay(retryDelayMs, cancellationToken);
            }
        }

        throw new InvalidOperationException($"Failed to send service task failure notification after {maxRetries} attempts");
    }

    /// <summary>
    /// Performs a health check on the SignalR connection
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the connection is healthy</returns>
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_hubConnection == null)
                return false;

            if (_hubConnection.State != HubConnectionState.Connected)
                return false;

            // Try to ping the server
            await _hubConnection.InvokeAsync("Ping", cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cleans up the connection resources
    /// </summary>
    private async Task CleanupConnectionAsync()
    {
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing SignalR connection");
            }
            finally
            {
                _hubConnection = null;
                _isConnected = false;
            }
        }
    }

    private void RegisterEventHandlers()
    {
        if (_hubConnection == null)
            return;

        _hubConnection.On<WorkerContext>("ProcessWork", async (workItem) =>
        {
            await HandleWorkRequestAsync(workItem);
        });

        _hubConnection.On("HealthCheck", async () =>
        {
            await HandleHealthCheckAsync();
        });

        _hubConnection.Reconnected += async (connectionId) =>
        {
            try
            {
                _logger.LogInformation("SignalR reconnected with ID: {ConnectionId}", connectionId);
                _isConnected = true; // Set connection status when reconnected

                // Re-register the client with the engine
                await RegisterClientAsync();

                _logger.LogInformation("Client successfully re-registered after reconnection");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to re-register client after reconnection");
                _isConnected = false; // Mark as disconnected if registration fails
            }
        };

        _hubConnection.Reconnecting += (exception) =>
        {
            _logger.LogWarning(exception, "SignalR reconnecting");
            return Task.CompletedTask;
        };

        _hubConnection.Closed += (exception) =>
        {
            _logger.LogWarning(exception, "SignalR connection closed");
            _isConnected = false;
            return Task.CompletedTask;
        };
    }

    private async Task HandleWorkRequestAsync(WorkerContext workItem)
    {
        try
        {
            _logger.LogInformation("Received work request: {WorkerId} of type {Implementation}, worker type: {WorkerType}",
                workItem.WorkerId, workItem.Implementation, workItem.WorkerType);

            // Ensure WorkerType is set (use Implementation if not set)
            if (string.IsNullOrEmpty(workItem.WorkerType))
            {
                workItem.WorkerType = workItem.Implementation;
            }

            // Find workers that can handle this work type
            var capableWorkers = _workerRegistry.GetWorkersForWorkType(workItem.WorkerType).ToList();

            if (!capableWorkers.Any())
            {
                _logger.LogWarning("No workers found for work type {Implementation}", workItem.Implementation);
                await SendWorkFailedAsync(workItem.WorkerId, $"No workers available for work type '{workItem.Implementation}'");
                return;
            }

            // Select the first available worker
            var selectedWorker = capableWorkers.FirstOrDefault(w => w.Enabled);
            if (selectedWorker == null)
            {
                _logger.LogWarning("No enabled workers found for work type {Implementation}", workItem.Implementation);
                await SendWorkFailedAsync(workItem.WorkerId, $"No enabled workers available for work type '{workItem.Implementation}'");
                return;
            }

            // Process the work
            if (selectedWorker.HandlerType == null)
            {
                await SendWorkFailedAsync(workItem.WorkerId, "Job handler type not configured");
                return;
            }

            var handler = (BpmnWorkerHandler)_serviceProvider.GetRequiredService(selectedWorker.HandlerType);

            // Convert JsonElement objects in variables to strings and populate BonyanVariables
            if (workItem.Variables != null && workItem.Variables.Any())
            {
                // Convert BonyanVariables to Dictionary for ConvertJsonElements
                var varsDict = workItem.Variables.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
                var convertedVars = ConvertJsonElements(varsDict);
                // Clear and repopulate BonyanVariables with string values
                workItem.Variables.Clear();
                foreach (var kvp in convertedVars)
                {
                    workItem.Variables.SetString(kvp.Key, ConvertValueToString(kvp.Value));
                }
            }

            // Execute the handler with properly typed variables
            await handler.ExecuteAsync(workItem);

            // Send completion notification
            await SendWorkCompletedAsync(workItem.WorkerId, new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing worker {WorkerId}", workItem.WorkerId);
            await SendWorkFailedAsync(workItem.WorkerId, ex.Message);
        }
    }

    private async Task HandleHealthCheckAsync()
    {
        try
        {
            await SendHeartbeatAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error responding to health check");
        }
    }

    private async Task RegisterClientAsync(CancellationToken cancellationToken = default)
    {
        if (_hubConnection == null || !_isConnected)
            return;

        var clientInfo = new
        {
            ClientId = _options.ClientId,
            SupportedWorkTypes = _workerRegistry.GetAllWorkers()
                .SelectMany(w => w.SupportedWorkTypes)
                .Distinct()
                .ToList(),
            RegisteredWorkers = _workerRegistry.GetAllWorkers().Count()
        };

        await _hubConnection.InvokeAsync("RegisterClient", clientInfo, cancellationToken);
        _logger.LogInformation("Client registered with engine: {ClientId}", _options.ClientId);
    }

    private async Task UnregisterClientAsync(CancellationToken cancellationToken = default)
    {
        if (_hubConnection == null || !_isConnected)
            return;

        try
        {
            await _hubConnection.InvokeAsync("UnregisterClient", _options.ClientId, cancellationToken);
            _logger.LogInformation("Client unregistered from engine: {ClientId}", _options.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering client from engine");
        }
    }

    private async Task HandleServiceTaskCompletedAckAsync(Guid workerId, Dictionary<string, string>? result)
    {
        try
        {
            _logger.LogInformation("Received service task completion acknowledgment for worker {WorkerId}", workerId);

            // Handle completion acknowledgment (could update local state, trigger events, etc.)
            // For now, just log the acknowledgment
            if (result != null && result.ContainsKey("status"))
            {
                _logger.LogInformation("Completion status: {Status}", result["status"]);
            }

            _logger.LogInformation("Service task completion acknowledgment processed for worker {WorkerId}", workerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing service task completion acknowledgment for worker {WorkerId}", workerId);
        }
    }

    private async Task HandleServiceTaskExecutionAsync(WorkerTaskRequest request)
    {
        try
        {
            _logger.LogInformation("Received service task execution request: {ExecutionId} - {TaskName}",
                request.ExecutionId, request.TaskName);

            // Parse implementation: split on "@" and use the second part if present
            // This handles cases where implementation might still contain routing info
            string actualWorkType = request.Implementation;
            if (!string.IsNullOrEmpty(request.Implementation) && request.Implementation.Contains("@"))
            {
                var parts = request.Implementation.Split('@', 2);
                if (parts.Length > 1)
                {
                    actualWorkType = parts[1]; // Use everything after the first "@"
                }
            }

            // Find a worker that can handle this service task
            var capableWorkers = _workerRegistry.GetWorkersForWorkType(actualWorkType);
            if (!capableWorkers.Any())
            {
                _logger.LogWarning("No workers found for service task execution");
                await SendServiceTaskFailedAsync(request.ExecutionId, "No workers available for service task execution");
                return;
            }

            // Select the first available worker
            var selectedWorker = capableWorkers.FirstOrDefault(w => w.Enabled);
            if (selectedWorker == null)
            {
                _logger.LogWarning("No enabled workers found for service task execution");
                await SendServiceTaskFailedAsync(request.ExecutionId, "No enabled workers available for service task execution");
                return;
            }

            // Execute the service task
            if (selectedWorker.HandlerType == null)
            {
                await SendServiceTaskFailedAsync(request.ExecutionId, "Job handler type not configured");
                return;
            }

            var handler = (BpmnWorkerHandler)_serviceProvider.GetRequiredService(selectedWorker.HandlerType);

            // Create a worker context for the service task (WorkerId is Guid, other IDs as strings, Variables as BonyanVariables)
            var workerContext = new WorkerContext
            {
                WorkerId = request.WorkerId != Guid.Empty ? request.WorkerId : request.ExecutionId,
                ProcessId = request.ProcessId.ToString(),
                TokenId = request.TokenId.ToString(),
                ElementId = request.ElementId ?? string.Empty,
                TaskName = request.TaskName ?? string.Empty,
                Implementation = request.Implementation ?? string.Empty,
                WorkerType = actualWorkType, // Set from the parsed work type
                Metadata = new Dictionary<string, string>
                {
                    ["ExecutionId"] = request.ExecutionId.ToString(),
                    ["IsServiceTask"] = "true"
                },
                Variables = new BonyanVariables()
            };

            // Copy variables from request (already strings, just copy directly)
            if (request.Variables != null && request.Variables.Any())
            {
                foreach (var kvp in request.Variables)
                {
                    workerContext.Variables.SetString(kvp.Key, kvp.Value);
                }
            }

            // Legacy support: also copy from Payload if Variables is empty
            if ((request.Variables == null || !request.Variables.Any()) && request.Payload != null)
            {
                foreach (var kvp in request.Payload)
                {
                    workerContext.Variables.SetString(kvp.Key, kvp.Value ?? string.Empty);
                }
            }

      

            // Execute the handler
            await handler.ExecuteAsync(workerContext);

            // Send completion notification with result (convert BonyanVariables to Dictionary<string, string>)
            var result = new Dictionary<string, string>
            {
                ["success"] = "true",
                ["executedBy"] = _options.ClientId ?? string.Empty,
                ["timestamp"] = DateTime.UtcNow.ToString("O")
            };

            // Include any results set by the handler from worker context variables
            foreach (var kvp in workerContext.Variables)
            {
                result[kvp.Key] = kvp.Value;
            }

            // Use worker ID from context
            await SendServiceTaskCompletedAsync(workerContext.WorkerId, result);

            _logger.LogInformation("Service task {ExecutionId} completed successfully", request.ExecutionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing service task {ExecutionId}", request.ExecutionId);

            try
            {
                // Try to send failure notification, but don't let this fail the entire operation
                await SendServiceTaskFailedAsync(request.ExecutionId, ex.Message);
            }
            catch (Exception sendEx)
            {
                _logger.LogError(sendEx, "Failed to send service task failure notification for {ExecutionId}", request.ExecutionId);
            }
        }
    }

    /// <summary>
    /// Converts JsonElement objects back to their actual .NET types
    /// </summary>
    /// <param name="value">The value that might be a JsonElement</param>
    /// <returns>The converted value as the actual .NET type</returns>
    private static object? ConvertJsonElement(object? value)
    {
        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.String => jsonElement.GetString(),
                JsonValueKind.Number => jsonElement.TryGetInt32(out var intValue) ? intValue :
                                       jsonElement.TryGetInt64(out var longValue) ? longValue :
                                       jsonElement.TryGetDouble(out var doubleValue) ? doubleValue :
                                       jsonElement.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Object => jsonElement.Deserialize<Dictionary<string, object>>() ?? new Dictionary<string, object>(),
                JsonValueKind.Array => jsonElement.Deserialize<List<object>>() ?? new List<object>(),
                _ => jsonElement.ToString()
            };
        }

        // If it's already a proper type, return as-is
        return value;
    }

    /// <summary>
    /// Converts all JsonElement objects in a dictionary to their actual .NET types
    /// </summary>
    /// <param name="variables">The dictionary with potentially JsonElement values</param>
    /// <returns>A new dictionary with converted values</returns>
    private static Dictionary<string, object> ConvertJsonElements(IDictionary<string, object>? variables)
    {
        if (variables == null)
            return new Dictionary<string, object>();

        var result = new Dictionary<string, object>();
        foreach (var kvp in variables)
        {
            result[kvp.Key] = ConvertJsonElement(kvp.Value);
        }
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupConnectionAsync();
    }
}
