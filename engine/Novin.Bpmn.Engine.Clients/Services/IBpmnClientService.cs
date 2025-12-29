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

    Task AssignUserTaskAsync(
        Guid workerId,
        AssignUserTaskRequest request,
        CancellationToken ct = default);

    Task CompleteUserTaskAsync(
        Guid workerId,
        CompleteTaskRequest request,
        CancellationToken ct = default);

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


public sealed record AssignUserTaskRequest(
    string? Assignee,
    string? CandidateGroups,
    int? Priority,
    DateTime? DueDateUtc,
    string AssignedBy
);

public sealed record CompleteTaskRequest(
    string CompletedBy,
    Dictionary<string, string> Result,
    string? Comment
);

public sealed record FailTaskRequest(
    string FailedBy,
    string ErrorMessage,
    string? ErrorCode
);
