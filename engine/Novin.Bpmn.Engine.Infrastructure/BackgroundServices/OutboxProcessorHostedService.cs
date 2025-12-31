using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that processes outbox messages.
/// Claims batches of messages and publishes them via MediatR.
/// </summary>
public class OutboxProcessorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessorHostedService> _logger;

    // Configuration
    private readonly TimeSpan _batchClaimLease = TimeSpan.FromMinutes(5);
    private readonly int _batchSize = 10;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(0.1);
    private readonly int _maxRetries = 5;

    public OutboxProcessorHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessorHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessorHostedService started. Processing interval: {Interval}s, Batch size: {BatchSize}",
            _processingInterval.TotalSeconds, _batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
                await Task.Delay(_processingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox batch");
                // Continue processing despite errors
                await Task.Delay(_processingInterval, stoppingToken);
            }
        }

        _logger.LogInformation("OutboxProcessorHostedService stopped");
    }

    /// <summary>
    /// Processes a batch of outbox messages
    /// </summary>
    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var claimer = scope.ServiceProvider.GetRequiredService<IOutboxBatchClaimer>();

        var messages = await claimer.ClaimAsync(_batchSize, _batchClaimLease, ct);
        if (messages.Count == 0) return;

        // سقف همزمانی (پیشنهاد: از options/config بخوان)
        var maxConcurrency = 8;
        using var sem = new SemaphoreSlim(maxConcurrency);

        var tasks = messages.Select(async msg =>
        {
            await sem.WaitAsync(ct);
            try
            {
                // ✅ scope جدا برای هر پیام (DbContext جدا)
                using var msgScope = _scopeFactory.CreateScope();
                var mediator = msgScope.ServiceProvider.GetRequiredService<IMediator>();
                var repository = msgScope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
                var jsonSerializer = msgScope.ServiceProvider.GetRequiredService<IJsonSerializer>();

                await ProcessMessageAsync(msg, mediator, repository, jsonSerializer, ct);
            }
            finally
            {
                sem.Release();
            }
        });

        await Task.WhenAll(tasks);
    }


    /// <summary>
    /// Processes a single outbox message
    /// </summary>
    private async Task ProcessMessageAsync(
        OutboxMessage message,
        IMediator mediator,
        IOutboxMessageRepository repository,
        IJsonSerializer jsonSerializer,
        CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Processing outbox message {MessageId} ({MessageName})",
                message.Id, message.MessageName);

            // Deserialize and publish the message
            var domainEvent = DeserializeDomainEvent(message, jsonSerializer);

            if (domainEvent != null)
            {
                await mediator.Publish(domainEvent, ct);

                // Mark as processed
                message.MarkAsProcessed(DateTime.UtcNow);
                _logger.LogDebug("Successfully processed outbox message {MessageId}", message.Id);
            }
            else
            {
                throw new InvalidOperationException($"Failed to deserialize message {message.Id}");
            }
        }
        catch (Exception ex)
        {
            await HandleProcessingErrorAsync(message, ex, repository, ct);
        }

        // Update the message status
        await repository.UpdateAsync(message, ct);
    }

    /// <summary>
    /// Deserializes a domain event from the outbox message payload
    /// </summary>
    private IDomainEvent? DeserializeDomainEvent(OutboxMessage message, IJsonSerializer jsonSerializer)
    {
        if (string.IsNullOrEmpty(message.MessageType))
        {
            return null;
        }

        try
        {
            var eventType = Type.GetType(message.MessageType);
            if (eventType == null)
            {
                return null;
            }

            // Use centralized JSON deserialization
            var domainEvent = jsonSerializer.DeserializeObject(message.Payload, eventType) as IDomainEvent;

            return domainEvent;
        }
        catch (Exception ex)
        {
            // Log the error for debugging
            Console.WriteLine($"Failed to deserialize domain event: {ex.Message}");
            return null;
        }
    }


    /// <summary>
    /// Handles processing errors by scheduling retries or marking as failed
    /// </summary>
    private async Task HandleProcessingErrorAsync(
        OutboxMessage message,
        Exception ex,
        IOutboxMessageRepository repository,
        CancellationToken ct)
    {
        var errorMessage = $"Processing failed: {ex.Message}";
        _logger.LogWarning(ex, "Failed to process outbox message {MessageId}: {Error}", message.Id, errorMessage);

        var currentAttempts = message.Attempts + 1;

        if (currentAttempts >= _maxRetries)
        {
            // Mark as permanently failed
            message.MarkAsFailed(errorMessage);
            _logger.LogError("Outbox message {MessageId} permanently failed after {Attempts} attempts",
                message.Id, currentAttempts);
        }
        else
        {
            // Schedule retry with exponential backoff
            var delay = TimeSpan.FromSeconds(Math.Pow(2, currentAttempts));
            var nextAttempt = DateTime.UtcNow.Add(delay);

            message.MarkAsFailed(errorMessage, nextAttempt);
            _logger.LogWarning("Scheduling retry for outbox message {MessageId} in {Delay}. Attempt {Attempt}/{MaxAttempts}",
                message.Id, delay, currentAttempts, _maxRetries);
        }
    }
}