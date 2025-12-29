using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Clients.Abstractions;

namespace Novin.Bpmn.Engine.Clients.Services;

/// <summary>
/// Main implementation of the BPMN client service
/// </summary>
public class BpmnClientService : IBpmnClientService
{
    private readonly BpmnClientOptions _options;
    private readonly IServiceWorkerRegistry _workerRegistry;
    private readonly IClientConnectionManager _connectionManager;
    private readonly ILogger<BpmnClientService> _logger;

    public BpmnClientService(
        BpmnClientOptions options,
        IServiceWorkerRegistry workerRegistry,
        IClientConnectionManager connectionManager,
        ILogger<BpmnClientService> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _workerRegistry = workerRegistry ?? throw new ArgumentNullException(nameof(workerRegistry));
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the client configuration options
    /// </summary>
    public BpmnClientOptions Options => _options;

    /// <summary>
    /// Registers a new work item for processing
    /// </summary>
    /// <param name="workItem">The work item to register</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task RegisterWorkItemAsync(WorkerContext workItem, CancellationToken cancellationToken = default)
    {
        if (workItem == null)
            throw new ArgumentNullException(nameof(workItem));

        _logger.LogInformation("Registering work item {WorkItemId} of type {WorkType}",
            workItem.WorkerId, workItem.WorkerType);

        // Find workers that can handle this work type
        var capableWorkers = _workerRegistry.GetWorkersForWorkType(workItem.WorkerType).ToList();

        if (!capableWorkers.Any())
        {
            _logger.LogWarning("No workers found for work type {WorkType}", workItem.WorkerType);
            throw new InvalidOperationException($"No workers available for work type '{workItem.WorkerType}'");
        }

        // Ensure the client is connected to the engine
        if (!_connectionManager.IsConnected())
        {
            _logger.LogWarning("Client is not connected to BPMN engine. Attempting to connect...");
            await _connectionManager.RegisterWithEngineAsync(cancellationToken);
        }

        // Work items are now processed through SignalR when received from the engine
        // This method primarily validates that workers exist for the work type
        _logger.LogInformation("Work item {WorkItemId} registered successfully. {WorkerCount} workers available.",
            workItem.WorkerId, capableWorkers.Count);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the status of the client and all its workers
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Client status information</returns>
    public async Task<ClientStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var allWorkers = _workerRegistry.GetAllWorkers().ToList();
        var activeWorkers = allWorkers.Count(w => w.Enabled);

        var status = new ClientStatus
        {
            ClientId = _options.ClientId,
            IsHealthy = true, // In a real implementation, this would check actual health
            RegisteredWorkers = allWorkers.Count,
            ActiveWorkers = activeWorkers,
            PendingWorkItems = 0, // In a real implementation, this would track actual queue
            ActiveWorkItems = 0,  // In a real implementation, this would track actual processing
            LastActivity = DateTime.UtcNow
        };

        _logger.LogDebug("Client status requested: {@Status}", status);

        return await Task.FromResult(status);
    }
    public async Task CompleteUserTaskAsync(
        Guid workerId,
        CompleteTaskRequest request,
        CancellationToken ct = default)
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            $"api/workers/{workerId}/complete-user",
            request,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Failed to complete user task {WorkerId}: {Status} {Body}",
                workerId,
                response.StatusCode,
                body);

            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation("UserTask {WorkerId} completed", workerId);
    }

    public async Task AssignUserTaskAsync(
        Guid workerId,
        AssignUserTaskRequest request,
        CancellationToken ct = default)
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            $"api/workers/{workerId}/assign",
            request,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Failed to assign user task {WorkerId}: {Status} {Body}",
                workerId,
                response.StatusCode,
                body);

            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation("UserTask {WorkerId} assigned", workerId);
    }
    public async Task CompleteServiceTaskAsync(
        Guid workerId,
        CompleteTaskRequest request,
        CancellationToken ct = default)
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            $"api/workers/{workerId}/complete-service",
            request,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Failed to complete service task {WorkerId}: {Status} {Body}",
                workerId,
                response.StatusCode,
                body);

            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation("ServiceTask {WorkerId} completed", workerId);
    }
    public async Task FailServiceTaskAsync(
        Guid workerId,
        FailTaskRequest request,
        CancellationToken ct = default)
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            $"api/workers/{workerId}/fail-service",
            request,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Failed to fail service task {WorkerId}: {Status} {Body}",
                workerId,
                response.StatusCode,
                body);

            response.EnsureSuccessStatusCode();
        }

        _logger.LogWarning("ServiceTask {WorkerId} failed", workerId);
    }

    private HttpClient CreateClient()
    {
        return new HttpClient
        {
            BaseAddress = new Uri(_options.EngineBaseUrl),
        };
    }

}


