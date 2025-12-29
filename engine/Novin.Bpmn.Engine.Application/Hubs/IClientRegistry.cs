namespace Novin.Bpmn.Engine.Application.Hubs;

/// <summary>
/// Interface for client registry
/// </summary>
public interface IClientRegistry
{
    Task RegisterClientAsync(RegisteredClient client);
    Task UnregisterClientAsync(string connectionId);
    Task<RegisteredClient?> GetClientByConnectionIdAsync(string connectionId);
    Task<RegisteredClient?> GetClientByIdAsync(string clientId);
    Task<IEnumerable<RegisteredClient>> GetAllClientsAsync();
    Task UpdateClientHeartbeatAsync(string connectionId);
}