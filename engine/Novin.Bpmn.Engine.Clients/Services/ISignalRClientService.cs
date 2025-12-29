using Novin.Bpmn.Engine.Clients.Abstractions;

namespace Novin.Bpmn.Engine.Clients.Services;

/// <summary>
/// Service for managing SignalR client connections to the BPMN engine
/// </summary>
public interface ISignalRClientService
{
    /// <summary>
    /// Starts the SignalR connection to the engine
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task StartConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the SignalR connection to the engine
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task StopConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a work completion notification to the engine
    /// </summary>
    /// <param name="workItemId">The work item ID</param>
    /// <param name="result">The work result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task SendWorkCompletedAsync(Guid workItemId, object result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a work failure notification to the engine
    /// </summary>
    /// <param name="workItemId">The work item ID</param>
    /// <param name="error">The error details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task SendWorkFailedAsync(Guid workItemId, string error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a heartbeat to the engine
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task SendHeartbeatAsync(CancellationToken cancellationToken = default);
}