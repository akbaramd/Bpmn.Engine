namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Service for managing token lifecycle operations (Retry, Move, Terminate)
/// These operations affect token state and process flow, unlike ResolveIncident which only changes incident status.
/// </summary>
public interface ITokenManagementService
{
    /// <summary>
    /// Retry a failed token: convert from Failed to Active and trigger processing
    /// Also retries the associated incident (increments retry count)
    /// </summary>
    Task RetryTokenAsync(Guid tokenId, CancellationToken ct = default);

    /// <summary>
    /// Move a token to a different element (manual correction)
    /// Token must be in Active or Failed state
    /// </summary>
    Task MoveTokenAsync(Guid tokenId, string targetElementId, CancellationToken ct = default);

    /// <summary>
    /// Terminate a token (cancel the branch)
    /// This can affect join gateways and may cause deadlocks if not used carefully
    /// </summary>
    Task TerminateTokenAsync(Guid tokenId, string? reason = null, CancellationToken ct = default);
}

