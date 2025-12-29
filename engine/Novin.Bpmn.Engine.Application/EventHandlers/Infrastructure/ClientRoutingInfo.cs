namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Client routing information parsed from BPMN extension elements
/// </summary>
public class ClientRoutingInfo
{
    /// <summary>
    /// Specific client ID to route to (null = broadcast to all)
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Clean implementation string without client routing info
    /// </summary>
    public string? CleanImplementation { get; set; }

    /// <summary>
    /// Timeout for execution (in seconds)
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Whether to require acknowledgment
    /// </summary>
    public bool RequireAcknowledgment { get; set; } = true;
}