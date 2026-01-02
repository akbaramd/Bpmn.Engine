using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
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
/// Zeebe-style Outbox Processor (partitioned, high-throughput):
/// - One poller claims batches fast
/// - Routes messages to fixed N partitions (hash(PartitionKey) => partition)
/// - Each partition is single-threaded => ordering preserved per PartitionKey
/// - Partitions run concurrently => no global "wait for slow message"
/// - Adaptive idle backoff with jitter (up to 3s), resets immediately on work
///
/// NOTE (important):
/// If your handlers do long work (30s script/service), that will still block the *same* partition.
/// The Zeebe way is: handler must be fast, long work => create "Job/Worker" + mark node waiting.
/// </summary>
public sealed class OutboxProcessorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessorHostedService> _logger;

    // ---- Tuning knobs (change via options if you want) ----
    private readonly TimeSpan _batchClaimLease = TimeSpan.FromMinutes(5);
    private readonly int _batchSize = 200;

    // Partitioning like Zeebe (fixed number of partitions)
    private readonly int _partitions;

    // Channels (bounded => backpressure, protects memory)
    private readonly int _channelCapacityPerPartition;

    // Idle backoff
    private readonly TimeSpan _minIdleDelay = TimeSpan.FromMilliseconds(5);
    private readonly TimeSpan _maxIdleDelay = TimeSpan.FromSeconds(3);

    // Observability
    private long _processed;
    private long _failed;

    private Channel<OutboxMessage>[] _partitionChannels = default!;
    private Task[] _partitionWorkers = default!;

    public OutboxProcessorHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessorHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // “Senior default”: CPU-bound-ish orchestration, but IO heavy handlers.
        // Keep partitions moderate: 2x cores (cap to 32) usually good.
        _partitions = Math.Clamp(Environment.ProcessorCount * 2, 4, 32);

        // Enough to keep pipe full, but bounded.
        _channelCapacityPerPartition = 1000;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        BuildPartitions();

        _logger.LogInformation(
            "OutboxProcessor started. Partitions={Partitions} Batch={Batch} Lease={Lease} ChanCap={ChanCap} Idle=[{MinMs}..{MaxMs}]ms",
            _partitions, _batchSize, _batchClaimLease, _channelCapacityPerPartition,
            _minIdleDelay.TotalMilliseconds, _maxIdleDelay.TotalMilliseconds);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Start workers
        for (var i = 0; i < _partitions; i++)
        {
            var p = i;
            _partitionWorkers[p] = Task.Run(() => PartitionLoopAsync(p, stoppingToken), stoppingToken);
        }

        // Poll + route loop
        await PollAndRouteLoopAsync(stoppingToken);

        // Graceful stop: complete channels, wait workers
        foreach (var ch in _partitionChannels)
            ch.Writer.TryComplete();

        try
        {
            await Task.WhenAll(_partitionWorkers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OutboxProcessor: workers crashed during shutdown.");
        }
    }

    private void BuildPartitions()
    {
        _partitionChannels = new Channel<OutboxMessage>[_partitions];
        _partitionWorkers = new Task[_partitions];

        for (var i = 0; i < _partitions; i++)
        {
            _partitionChannels[i] = Channel.CreateBounded<OutboxMessage>(
                new BoundedChannelOptions(_channelCapacityPerPartition)
                {
                    SingleReader = true,   // each partition has exactly one reader => ordered
                    SingleWriter = false,  // poller is single, but safe if future writers added
                    AllowSynchronousContinuations = true,
                    FullMode = BoundedChannelFullMode.Wait // backpressure
                });
            _partitionWorkers[i] = Task.CompletedTask;
        }
    }

    private async Task PollAndRouteLoopAsync(CancellationToken ct)
    {
        var idleDelay = _minIdleDelay;

        // Small trick: if channels are saturated, we should avoid hammering DB
        // and let workers catch up.
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // If queues are “too full”, yield quickly (micro backoff)
                if (IsBackpressured())
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1), ct);
                    continue;
                }

                IReadOnlyList<OutboxMessage> batch;

                // scope جدا برای claim (DbContext جدا)
                using (var scope = _scopeFactory.CreateScope())
                {
                    var claimer = scope.ServiceProvider.GetRequiredService<IOutboxBatchClaimer>();
                    batch = await claimer.ClaimAsync(_batchSize, _batchClaimLease, ct);
                }

                if (batch.Count == 0)
                {
                    // idle => exponential backoff + jitter (cap 3s)
                    await Task.Delay(Backoff.Next(idleDelay, _minIdleDelay, _maxIdleDelay), ct);
                    idleDelay = Backoff.Grow(idleDelay, _minIdleDelay, _maxIdleDelay);
                    continue;
                }

                // got work => reset delay immediately
                idleDelay = _minIdleDelay;

                // Route quickly to partitions (preserve per-key ordering via stable partition)
                // IMPORTANT: ClaimAsync should already provide stable ordering per PartitionKey if you care.
                foreach (var msg in batch)
                {
                    ct.ThrowIfCancellationRequested();

                    var key = GetPartitionKey(msg);
                    var p = PickPartition(key, _partitions);

                    // Backpressure: await if partition queue is full
                    await _partitionChannels[p].Writer.WriteAsync(msg, ct);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxProcessor: error in poller loop.");
                await Task.Delay(_minIdleDelay, ct);
            }
        }
    }

    private bool IsBackpressured()
    {
        // cheap heuristic: if ANY partition is near full, reduce DB pressure a bit
        // Channel doesn't expose length; so we rely on TryWrite behavior indirectly (not here).
        // We'll keep it always false for now; bounded channel already enforces backpressure on WriteAsync.
        return false;
    }

    private async Task PartitionLoopAsync(int partition, CancellationToken ct)
    {
        var reader = _partitionChannels[partition].Reader;

        // Single-threaded per partition => ordering guarantee for that partition.
        while (!ct.IsCancellationRequested && await reader.WaitToReadAsync(ct))
        {
            while (reader.TryRead(out var msg))
            {
                await ProcessOneMessageInOwnScopeAsync(msg, partition, ct);
            }
        }
    }

    private async Task ProcessOneMessageInOwnScopeAsync(OutboxMessage message, int partition, CancellationToken ct)
    {
        // Separate scope per message => separate DbContext, avoids tracking growth, avoids cross-message leaks.
        using var scope = _scopeFactory.CreateScope();

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var jsonSerializer = scope.ServiceProvider.GetRequiredService<IJsonSerializer>();

        var sw = Stopwatch.StartNew();

        try
        {
            var domainEvent = DeserializeDomainEvent(message, jsonSerializer);
            if (domainEvent is null)
                throw new InvalidOperationException($"Failed to deserialize message {message.Id}");

            // IMPORTANT:
            // This is where your system can "hang" if handler does long work.
            // Handlers MUST be fast. Long work => create worker/job + return.
            await mediator.Publish(domainEvent, ct);

            message.MarkAsProcessed(DateTime.UtcNow);
            Interlocked.Increment(ref _processed);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failed);
            await HandleProcessingErrorAsync(message, ex, repository, ct);

            _logger.LogWarning(ex,
                "OutboxProcessor: message failed. MsgId={MsgId} Name={Name} Part={Part} Attempts={Attempts} TookMs={Ms}",
                message.Id, message.MessageName, partition, message.Attempts, sw.ElapsedMilliseconds);
        }

        // Persist outbox state
        await repository.UpdateAsync(message, ct);

        sw.Stop();

        if (sw.ElapsedMilliseconds > 5000)
        {
            _logger.LogWarning(
                "OutboxProcessor: slow message. MsgId={MsgId} Name={Name} Part={Part} TookMs={Ms} Key={Key}",
                message.Id, message.MessageName, partition, sw.ElapsedMilliseconds, GetPartitionKey(message));
        }
    }

    private IDomainEvent? DeserializeDomainEvent(OutboxMessage message, IJsonSerializer jsonSerializer)
    {
        if (string.IsNullOrWhiteSpace(message.MessageType))
            return null;

        try
        {
            var eventType = Type.GetType(message.MessageType);
            if (eventType is null) return null;

            return jsonSerializer.DeserializeObject(message.Payload, eventType) as IDomainEvent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OutboxProcessor: failed to deserialize. MsgId={MsgId}", message.Id);
            return null;
        }
    }

    private async Task HandleProcessingErrorAsync(
        OutboxMessage message,
        Exception ex,
        IOutboxMessageRepository repository,
        CancellationToken ct)
    {
        // Zeebe-ish: deterministic retry w/ exponential backoff; do not block pipeline.
        var currentAttempts = message.Attempts + 1;
        const int maxRetries = 5;

        var errorMessage = $"Processing failed: {ex.Message}";

        if (currentAttempts >= maxRetries)
        {
            message.MarkAsFailed(errorMessage);
            return;
        }

        // Exponential backoff (1s, 2s, 4s, 8s, 16s) + jitter
        var delay = TimeSpan.FromSeconds(Math.Pow(2, currentAttempts));
        delay = Backoff.Jitter(delay, 0.25); // 25% jitter

        var nextAttempt = DateTime.UtcNow.Add(delay);

        // MarkAsFailed(nextAttempt) means: failed but scheduled to retry
        message.MarkAsFailed(errorMessage, nextAttempt);

        await Task.CompletedTask;
    }

    private static string GetPartitionKey(OutboxMessage msg)
    {
        // BEST: have a real column on OutboxMessage: PartitionKey
        // Use ProcessId-based key for BPMN ordering:
        //   PartitionKey = $"proc:{ProcessId:N}"
        // Or token scope:
        //   PartitionKey = $"scope:{ScopeId:N}"
        //
        // Here: reflection fallback, but you SHOULD make it a real property.
        var pkProp = msg.GetType().GetProperty("PartitionKey");
        var pk = pkProp?.GetValue(msg) as string;

        if (!string.IsNullOrWhiteSpace(pk))
            return pk;

        // Worst-case: global ordering (everything serial for correct behavior)
        return "global";
    }

    private static int PickPartition(string key, int partitions)
    {
        // Stable, fast hash (avoid string.GetHashCode which is randomized per process).
        // FNV-1a 32-bit
        unchecked
        {
            uint hash = 2166136261;
            for (var i = 0; i < key.Length; i++)
            {
                hash ^= key[i];
                hash *= 16777619;
            }
            return (int)(hash % (uint)partitions);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "OutboxProcessor stopping... Processed={Processed} Failed={Failed}",
            Interlocked.Read(ref _processed), Interlocked.Read(ref _failed));

        await base.StopAsync(cancellationToken);
    }

    private static class Backoff
    {
        private static readonly ThreadLocal<Random> Rng = new(() => new Random());

        public static TimeSpan Grow(TimeSpan current, TimeSpan min, TimeSpan max)
        {
            var nextMs = Math.Min(max.TotalMilliseconds, Math.Max(min.TotalMilliseconds, current.TotalMilliseconds * 2));
            return TimeSpan.FromMilliseconds(nextMs);
        }

        public static TimeSpan Next(TimeSpan current, TimeSpan min, TimeSpan max)
            => Jitter(current < min ? min : (current > max ? max : current), 0.20);

        public static TimeSpan Jitter(TimeSpan baseDelay, double jitterRatio)
        {
            // jitterRatio=0.2 => +/-20%
            var r = Rng.Value!;
            var factor = 1.0 + ((r.NextDouble() * 2.0) - 1.0) * jitterRatio;
            var ms = Math.Max(0, baseDelay.TotalMilliseconds * factor);
            return TimeSpan.FromMilliseconds(ms);
        }
    }
}
