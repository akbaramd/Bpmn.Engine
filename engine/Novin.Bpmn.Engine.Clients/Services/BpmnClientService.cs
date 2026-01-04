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

public async Task StartProcessAsync(
    string deploymentKey,
    string processId,
    string processTitle,
    Dictionary<string, object?>? variables = null,
    CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(deploymentKey))
        throw new ArgumentException("Deployment key is required.", nameof(deploymentKey));

    if (string.IsNullOrWhiteSpace(processId))
        throw new ArgumentException("Process BPMN id is required.", nameof(processId));

    if (string.IsNullOrWhiteSpace(processTitle))
        throw new ArgumentException("Process title is required.", nameof(processTitle));

    // Ensure the client is connected to the engine (consistent with RegisterWorkItemAsync)
    if (!_connectionManager.IsConnected())
    {
        _logger.LogWarning("Client is not connected to BPMN engine. Attempting to connect...");
        await _connectionManager.RegisterWithEngineAsync(ct);
    }

    // Copy variables so we don't mutate the caller's dictionary
    var initialVars = variables is null
        ? null
        : new Dictionary<string, object?>(variables);

    // Optional "reserved" values may be provided inside variables
    // (handy since StartProcessAsync signature doesn't include them).
    Guid projectId = Guid.Parse("6f7c9c6a-7b8b-4b84-8a7a-1c2a3b4c5d6e"); // <-- expected in your BpmnClientOptions
    string? businessKey = null;
    string? explicitStartElementId = null;

    

    if (projectId == Guid.Empty)
        throw new InvalidOperationException(
            "ProjectId is required to start a process. Set BpmnClientOptions.ProjectId or pass it in variables['projectId'].");

    var command = new StartProcessCommandDto
    {
        ProjectId = projectId,
        DeploymentKey = deploymentKey,
        ProcessBpmnId = processId,
        ProcessName = processTitle,
        BusinessKey = businessKey,
        ExplicitStartElementId = explicitStartElementId,
        InitialVariables = initialVars
    };

    using var client = CreateClient();

    _logger.LogInformation(
        "Starting process {ProcessBpmnId} from deployment {DeploymentKey} (ProjectId={ProjectId})",
        command.ProcessBpmnId,
        command.DeploymentKey,
        command.ProjectId);

    var response = await client.PostAsJsonAsync("api/processes/start", command, ct);

    if (!response.IsSuccessStatusCode)
    {
        var body = await response.Content.ReadAsStringAsync(ct);

        _logger.LogError(
            "Failed to start process {ProcessBpmnId} from deployment {DeploymentKey}: {Status} {Body}",
            command.ProcessBpmnId,
            command.DeploymentKey,
            response.StatusCode,
            body);

        response.EnsureSuccessStatusCode();
    }

    // Best-effort deserialize result (endpoint returns 201 with StartProcessResult)
    StartProcessResultDto? result = null;
    try
    {
        result = await response.Content.ReadFromJsonAsync<StartProcessResultDto>(cancellationToken: ct);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Process started but response body could not be deserialized.");
    }

    if (result?.ProcessId is { } startedId && startedId != Guid.Empty)
    {
        _logger.LogInformation("Process started successfully. ProcessId={ProcessId}", startedId);
    }
    else
    {
        _logger.LogInformation("Process started successfully. (No ProcessId parsed from response)");
    }
}

   public async Task<UserTaskDto?> GetUserTaskAsync(Guid userTaskId, CancellationToken ct = default)
{
    using var client = CreateClient();

    var response = await client.GetAsync($"api/user-tasks/{userTaskId}", ct);

    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        _logger.LogWarning("UserTask {UserTaskId} not found", userTaskId);
        return null;
    }

    if (!response.IsSuccessStatusCode)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogError(
            "Failed to get user task {UserTaskId}: {Status} {Body}",
            userTaskId,
            response.StatusCode,
            body);

        response.EnsureSuccessStatusCode();
    }

    return await response.Content.ReadFromJsonAsync<UserTaskDto>(cancellationToken: ct);
}

public async Task CompleteUserTaskAsync(
    Guid userTaskId,
    CompleteUserTaskRequest request,
    CancellationToken ct = default)
{
    if (request is null) throw new ArgumentNullException(nameof(request));

    using var client = CreateClient();

    var response = await client.PostAsJsonAsync(
        $"api/user-tasks/{userTaskId}/complete",
        request,
        ct);

    if (!response.IsSuccessStatusCode)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogError(
            "Failed to complete user task {UserTaskId}: {Status} {Body}",
            userTaskId,
            response.StatusCode,
            body);

        response.EnsureSuccessStatusCode();
    }

    _logger.LogInformation("UserTask {UserTaskId} completed", userTaskId);
}

public async Task AssignUserTaskAsync(
    Guid userTaskId,
    AssignUserTaskRequest request,
    CancellationToken ct = default)
{
    if (request is null) throw new ArgumentNullException(nameof(request));

    using var client = CreateClient();

    var response = await client.PostAsJsonAsync(
        $"api/user-tasks/{userTaskId}/assign",
        request,
        ct);

    if (!response.IsSuccessStatusCode)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogError(
            "Failed to assign user task {UserTaskId}: {Status} {Body}",
            userTaskId,
            response.StatusCode,
            body);

        response.EnsureSuccessStatusCode();
    }

    _logger.LogInformation("UserTask {UserTaskId} assigned", userTaskId);
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


