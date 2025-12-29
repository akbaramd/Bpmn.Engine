namespace Novin.Bpmn.Engine.Application.Hubs;

/// <summary>
/// Registered client information
/// </summary>
public class RegisteredClient
{
    public string ClientId { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public List<string> SupportedWorkTypes { get; set; } = new();
    public int RegisteredWorkers { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime LastHeartbeat { get; set; }
}