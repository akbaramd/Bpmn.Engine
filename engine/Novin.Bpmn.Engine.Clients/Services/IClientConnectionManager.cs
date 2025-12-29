namespace Novin.Bpmn.Engine.Clients.Services;

/// <summary>
/// Manages client connections to the BPMN engine
/// </summary>
public interface IClientConnectionManager
{
    /// <summary>
    /// Registers the client with the BPMN engine
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task RegisterWithEngineAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregisters the client from the BPMN engine
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task UnregisterFromEngineAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the client is currently connected to the engine
    /// </summary>
    /// <returns>True if connected</returns>
    bool IsConnected();

    /// <summary>
    /// Gets the connection status
    /// </summary>
    /// <returns>Connection status information</returns>
    ConnectionStatus GetConnectionStatus();
}

/// <summary>
/// Represents the connection status of a client
/// </summary>
public class ConnectionStatus
{
    /// <summary>
    /// Whether the client is connected
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Connection ID if connected
    /// </summary>
    public string? ConnectionId { get; set; }

    /// <summary>
    /// Last connection attempt timestamp
    /// </summary>
    public DateTime? LastConnectionAttempt { get; set; }

    /// <summary>
    /// Last successful connection timestamp
    /// </summary>
    public DateTime? LastConnectedAt { get; set; }

    /// <summary>
    /// Last disconnection timestamp
    /// </summary>
    public DateTime? LastDisconnectedAt { get; set; }

    /// <summary>
    /// Connection error message if any
    /// </summary>
    public string? ErrorMessage { get; set; }
}