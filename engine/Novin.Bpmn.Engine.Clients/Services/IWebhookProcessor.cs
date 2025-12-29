using Novin.Bpmn.Engine.Clients.Abstractions;

namespace Novin.Bpmn.Engine.Clients.Services;

/// <summary>
/// Processes webhook callbacks from the BPMN engine
/// </summary>
public interface IWebhookProcessor
{
    /// <summary>
    /// Processes a webhook payload
    /// </summary>
    /// <param name="webhookData">The webhook payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task ProcessWebhookAsync(WebhookData webhookData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a webhook signature (if implemented)
    /// </summary>
    /// <param name="payload">The webhook payload</param>
    /// <param name="signature">The signature to validate</param>
    /// <returns>True if the signature is valid</returns>
    bool ValidateWebhookSignature(string payload, string signature);
}

/// <summary>
/// Represents webhook data received from the BPMN engine
/// </summary>
public class WebhookData
{
    /// <summary>
    /// Unique identifier for the webhook event
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Type of webhook event
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// The source BPMN engine instance
    /// </summary>
    public string SourceEngine { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the event occurred
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The webhook payload data
    /// </summary>
    public Dictionary<string, string> Payload { get; set; } = new();

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}