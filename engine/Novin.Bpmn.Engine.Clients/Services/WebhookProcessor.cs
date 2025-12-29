using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Clients.Abstractions;

namespace Novin.Bpmn.Engine.Clients.Services;

/// <summary>
/// Processes webhook callbacks from the BPMN engine
/// </summary>
public class WebhookProcessor : IWebhookProcessor
{
    private readonly BpmnClientOptions _options;
    private readonly IServiceWorkerRegistry _workerRegistry;
    private readonly IBpmnClientService _clientService;
    private readonly ILogger<WebhookProcessor> _logger;
    private readonly IJsonSerializer _jsonSerializer;

    public WebhookProcessor(
        BpmnClientOptions options,
        IServiceWorkerRegistry workerRegistry,
        IBpmnClientService clientService,
        ILogger<WebhookProcessor> logger,
        IJsonSerializer jsonSerializer)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _workerRegistry = workerRegistry ?? throw new ArgumentNullException(nameof(workerRegistry));
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
    }

    /// <summary>
    /// Processes a webhook payload
    /// </summary>
    /// <param name="webhookData">The webhook payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task ProcessWebhookAsync(WebhookData webhookData, CancellationToken cancellationToken = default)
    {
        if (webhookData == null)
            throw new ArgumentNullException(nameof(webhookData));

        _logger.LogInformation("Processing webhook {EventId} of type {EventType}",
            webhookData.EventId, webhookData.EventType);

        try
        {
            // Handle different webhook event types
            switch (webhookData.EventType.ToLowerInvariant())
            {
                case "work.available":
                    await HandleWorkAvailableAsync(webhookData, cancellationToken);
                    break;

                case "process.completed":
                    await HandleProcessCompletedAsync(webhookData, cancellationToken);
                    break;

                case "process.failed":
                    await HandleProcessFailedAsync(webhookData, cancellationToken);
                    break;

                case "task.assigned":
                    await HandleTaskAssignedAsync(webhookData, cancellationToken);
                    break;

                default:
                    _logger.LogWarning("Unknown webhook event type: {EventType}", webhookData.EventType);
                    break;
            }

            _logger.LogInformation("Webhook {EventId} processed successfully", webhookData.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook {EventId}", webhookData.EventId);
            throw;
        }
    }

    /// <summary>
    /// Validates a webhook signature (if implemented)
    /// </summary>
    /// <param name="payload">The webhook payload</param>
    /// <param name="signature">The signature to validate</param>
    /// <returns>True if the signature is valid</returns>
    public bool ValidateWebhookSignature(string payload, string signature)
    {
        // Basic implementation - in production, this should use proper cryptographic validation
        // For now, just return true (no validation)
        _logger.LogWarning("Webhook signature validation not implemented");
        return true;
    }

    private async Task HandleWorkAvailableAsync(WebhookData webhookData, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling work available webhook");

        // Extract work item from payload
        if (webhookData.Payload.TryGetValue("workItem", out var workItemData))
        {
            try
            {
                var workItemJson = _jsonSerializer.SerializeObject(workItemData);
                var workItem = _jsonSerializer.DeserializeObject<WorkerContext>(workItemJson);

                if (workItem != null)
                {
                    await _clientService.RegisterWorkItemAsync(workItem, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize work item from webhook payload");
            }
        }
    }

    private async Task HandleProcessCompletedAsync(WebhookData webhookData, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling process completed webhook");

        // Extract process information and notify relevant workers
        if (webhookData.Payload.TryGetValue("processId", out var processId))
        {
            _logger.LogInformation("Process {ProcessId} completed", processId);
            // Additional processing logic would go here
        }
    }

    private async Task HandleProcessFailedAsync(WebhookData webhookData, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling process failed webhook");

        // Extract process information and handle failure
        if (webhookData.Payload.TryGetValue("processId", out var processId) &&
            webhookData.Payload.TryGetValue("error", out var error))
        {
            _logger.LogError("Process {ProcessId} failed with error: {Error}", processId, error);
            // Additional error handling logic would go here
        }
    }

    private async Task HandleTaskAssignedAsync(WebhookData webhookData, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling task assigned webhook");

        // Extract task information and route to appropriate worker
        if (webhookData.Payload.TryGetValue("taskId", out var taskId) &&
            webhookData.Payload.TryGetValue("taskType", out var taskType))
        {
            _logger.LogInformation("Task {TaskId} of type {TaskType} assigned", taskId, taskType);

            // Find workers that can handle this task type
            var capableWorkers = _workerRegistry.GetWorkersForWorkType(taskType?.ToString() ?? "");
            if (capableWorkers.Any())
            {
                _logger.LogInformation("Found {WorkerCount} workers for task type {TaskType}",
                    capableWorkers.Count(), taskType);
                // Task assignment logic would go here
            }
        }
    }
}