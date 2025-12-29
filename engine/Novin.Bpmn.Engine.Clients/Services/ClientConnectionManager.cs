using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Clients.Abstractions;

namespace Novin.Bpmn.Engine.Clients.Services;

/// <summary>
/// Manages client connections to the BPMN engine
/// </summary>
public class ClientConnectionManager : IClientConnectionManager
{
    private readonly BpmnClientOptions _options;
    private readonly ISignalRClientService _signalRService;
    private readonly ILogger<ClientConnectionManager> _logger;

    private ConnectionStatus _connectionStatus = new();

    public ClientConnectionManager(
        BpmnClientOptions options,
        ISignalRClientService signalRService,
        ILogger<ClientConnectionManager> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _signalRService = signalRService ?? throw new ArgumentNullException(nameof(signalRService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Registers the client with the BPMN engine
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task RegisterWithEngineAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _connectionStatus.LastConnectionAttempt = DateTime.UtcNow;
            _logger.LogInformation("Registering client {ClientId} with BPMN engine at {EngineUrl}",
                _options.ClientId, _options.EngineBaseUrl);

            await _signalRService.StartConnectionAsync(cancellationToken);

            _connectionStatus.IsConnected = true;
            _connectionStatus.LastConnectedAt = DateTime.UtcNow;
            _connectionStatus.ErrorMessage = null;

            _logger.LogInformation("Client {ClientId} successfully registered with BPMN engine",
                _options.ClientId);
        }
        catch (Exception ex)
        {
            _connectionStatus.IsConnected = false;
            _connectionStatus.LastDisconnectedAt = DateTime.UtcNow;
            _connectionStatus.ErrorMessage = ex.Message;

            _logger.LogError(ex, "Failed to register client {ClientId} with BPMN engine",
                _options.ClientId);
            throw;
        }
    }

    /// <summary>
    /// Unregisters the client from the BPMN engine
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task UnregisterFromEngineAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Unregistering client {ClientId} from BPMN engine",
                _options.ClientId);

            await _signalRService.StopConnectionAsync(cancellationToken);

            _connectionStatus.IsConnected = false;
            _connectionStatus.LastDisconnectedAt = DateTime.UtcNow;
            _connectionStatus.ErrorMessage = null;

            _logger.LogInformation("Client {ClientId} successfully unregistered from BPMN engine",
                _options.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering client {ClientId} from BPMN engine",
                _options.ClientId);
            throw;
        }
    }

    /// <summary>
    /// Checks if the client is currently connected to the engine
    /// </summary>
    /// <returns>True if connected</returns>
    public bool IsConnected()
    {
        return _connectionStatus.IsConnected;
    }

    /// <summary>
    /// Gets the connection status
    /// </summary>
    /// <returns>Connection status information</returns>
    public ConnectionStatus GetConnectionStatus()
    {
        return _connectionStatus;
    }
}