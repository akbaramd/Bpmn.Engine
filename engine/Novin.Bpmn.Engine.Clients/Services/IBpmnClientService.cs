using Novin.Bpmn.Engine.Clients.Abstractions;

namespace Novin.Bpmn.Engine.Clients.Services;

/// <summary>
/// Main service interface for BPMN engine client operations
/// </summary>
public interface IBpmnClientService
{
    BpmnClientOptions Options { get; }

    Task RegisterWorkItemAsync(WorkerContext workItem, CancellationToken ct = default);

    Task<ClientStatus> GetStatusAsync(CancellationToken ct = default);

    // 🔽 NEW

 
    Task StartProcessAsync(
        string deploymentKey,
        string processId,
        string processTitle,
        Dictionary<string, object?>? variables = null,
        CancellationToken ct = default);

    // ✅ USER TASKS
    Task<UserTaskDto?> GetUserTaskAsync(Guid userTaskId, CancellationToken ct = default);

    Task AssignUserTaskAsync(Guid userTaskId, AssignUserTaskRequest request, CancellationToken ct = default);

    Task CompleteUserTaskAsync(Guid userTaskId, CompleteUserTaskRequest request, CancellationToken ct = default);

    Task CompleteServiceTaskAsync(
        Guid workerId,
        CompleteTaskRequest request,
        CancellationToken ct = default);

    Task FailServiceTaskAsync(
        Guid workerId,
        FailTaskRequest request,
        CancellationToken ct = default);
}


/// <summary>
/// Represents the current status of a BPMN client
/// </summary>
public class ClientStatus
{
    /// <summary>
    /// Client identifier
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the client is healthy
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Number of registered workers
    /// </summary>
    public int RegisteredWorkers { get; set; }

    /// <summary>
    /// Number of active workers
    /// </summary>
    public int ActiveWorkers { get; set; }

    /// <summary>
    /// Number of pending work items
    /// </summary>
    public int PendingWorkItems { get; set; }

    /// <summary>
    /// Number of actively processing work items
    /// </summary>
    public int ActiveWorkItems { get; set; }

    /// <summary>
    /// Last activity timestamp
    /// </summary>
    public DateTime LastActivity { get; set; }
}


public sealed record CompleteUserTaskRequest(
    Dictionary<string, string>? Result,
    string? Comment
);

// You can keep your existing AssignUserTaskRequest (extra fields are ignored by server by default),
// but I recommend making AssignedBy optional since server uses GetActor().
public sealed record AssignUserTaskRequest(
    string? Assignee,
    string? CandidateGroups,
    int? Priority,
    DateTime? DueDateUtc,
    string? AssignedBy = null
);

public sealed record UserTaskDto(
    Guid UserTaskId,
    string Type,
    string Status,
    string? Assignee,
    DateTimeOffset CreatedAt,
    Dictionary<string, object?>? Payload
);

public sealed record CompleteTaskRequest(
    string CompletedBy,
    Dictionary<string, object?> Variables,
    string? Comment
);

public sealed record FailTaskRequest(
    string FailedBy,
    string ErrorMessage,
    string? ErrorCode
);


/// <summary>
/// Client-side DTO matching the API StartProcessCommand shape.
/// Use your shared contract type instead if you already reference it.
/// </summary>
public sealed class StartProcessCommandDto
{
    public Guid ProjectId { get; init; }
    public string DeploymentKey { get; init; } = default!;
    public string? ProcessBpmnId { get; init; }
    public string? BusinessKey { get; init; }
    public IDictionary<string, object?>? InitialVariables { get; init; }
    public string? ExplicitStartElementId { get; init; }
    public string? ProcessName { get; init; }
}

/// <summary>
/// Minimal client-side DTO for the API result.
/// Extend with other properties your StartProcessResult includes.
/// </summary>
public sealed class StartProcessResultDto
{
    public Guid ProcessId { get; init; }
}