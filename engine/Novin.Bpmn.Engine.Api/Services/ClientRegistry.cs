using System.Collections.Concurrent;
using Novin.Bpmn.Engine.Application.Hubs;

namespace Novin.Bpmn.Engine.Api.Services;

/// <summary>
/// In-memory client registry implementation
/// </summary>
public class ClientRegistry : IClientRegistry
{
    private readonly ConcurrentDictionary<string, RegisteredClient> _clientsByConnectionId = new();
    private readonly ConcurrentDictionary<string, RegisteredClient> _clientsById = new();
    private readonly ILogger<ClientRegistry> _logger;

    public ClientRegistry(ILogger<ClientRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Registers a client
    /// </summary>
    /// <param name="client">The client to register</param>
    /// <returns>A task representing the asynchronous operation</returns>                    
    public async Task RegisterClientAsync(RegisteredClient client)
    {
        if (client == null)
            throw new ArgumentNullException(nameof(client));

        if (string.IsNullOrWhiteSpace(client.ClientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(client.ClientId));

        if (string.IsNullOrWhiteSpace(client.ConnectionId))
            throw new ArgumentException("Connection ID cannot be null or empty", nameof(client.ConnectionId));

        // Remove any existing registration for this client ID
        if (_clientsById.TryRemove(client.ClientId, out var existingClient))
        {
            _clientsByConnectionId.TryRemove(existingClient.ConnectionId, out _);
            _logger.LogInformation("Removed existing registration for client {ClientId}", client.ClientId);
        }

        // Register the new client
        _clientsByConnectionId[client.ConnectionId] = client;
        _clientsById[client.ClientId] = client;

        _logger.LogInformation("Client registered: {ClientId} (Connection: {ConnectionId})",
            client.ClientId, client.ConnectionId);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Unregisters a client by connection ID
    /// </summary>
    /// <param name="connectionId">The connection ID</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task UnregisterClientAsync(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
            return;

        if (_clientsByConnectionId.TryRemove(connectionId, out var client))
        {
            _clientsById.TryRemove(client.ClientId, out _);
            _logger.LogInformation("Client unregistered: {ClientId} (Connection: {ConnectionId})",
                client.ClientId, connectionId);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets a client by connection ID
    /// </summary>
    /// <param name="connectionId">The connection ID</param>
    /// <returns>The client or null if not found</returns>
    public async Task<RegisteredClient?> GetClientByConnectionIdAsync(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
            return null;

        _clientsByConnectionId.TryGetValue(connectionId, out var client);
        return await Task.FromResult(client);
    }

    /// <summary>
    /// Gets a client by client ID
    /// </summary>
    /// <param name="clientId">The client ID</param>
    /// <returns>The client or null if not found</returns>
    public async Task<RegisteredClient?> GetClientByIdAsync(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return null;

        _clientsById.TryGetValue(clientId, out var client);
        return await Task.FromResult(client);
    }

    /// <summary>
    /// Gets all registered clients
    /// </summary>
    /// <returns>All registered clients</returns>
    public async Task<IEnumerable<RegisteredClient>> GetAllClientsAsync()
    {
        return await Task.FromResult(_clientsById.Values.ToList());
    }

    /// <summary>
    /// Updates the heartbeat timestamp for a client
    /// </summary>
    /// <param name="connectionId">The connection ID</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task UpdateClientHeartbeatAsync(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
            return;

        if (_clientsByConnectionId.TryGetValue(connectionId, out var client))
        {
            client.LastHeartbeat = DateTime.UtcNow;
            _logger.LogDebug("Heartbeat updated for client {ClientId}", client.ClientId);
        }

        await Task.CompletedTask;
    }
}