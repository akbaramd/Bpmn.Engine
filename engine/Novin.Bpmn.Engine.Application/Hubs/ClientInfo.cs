namespace Novin.Bpmn.Engine.Application.Hubs;

/// <summary>
/// Client information for registration
/// </summary>
public class ClientInfo
{
    public string ClientId { get; set; } = string.Empty;
    public List<string>? SupportedWorkTypes { get; set; }
    public int RegisteredWorkers { get; set; }
}