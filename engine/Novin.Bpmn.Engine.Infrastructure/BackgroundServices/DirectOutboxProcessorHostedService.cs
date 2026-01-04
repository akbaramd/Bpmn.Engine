using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;
using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Infrastructure.Outbox.Direct;

/// <summary>
/// Direct outbox processor (NO Redis):
/// 1) Claim batch from outbox store (Elastic via IOutboxBatchClaimer)
/// 2) Deserialize + Publish via MediatR
/// 3) Bulk mark processed/failed via IOutboxStateStore
///
/// This removes Redis from the pipeline (lower latency, fewer moving parts).
/// </summary>
public sealed class DirectOutboxProcessorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DirectOutboxProcessorHostedService> _logger;

    // Tuning
    private readonly int _batchSize = 200;
    private readonly TimeSpan _lease = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _idleDelay = TimeSpan.FromMilliseconds(10);
    private readonly int _maxInFlight = 16; // bounded parallelism inside a batch

    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    private static readonly ConcurrentDictionary<string, Type?> TypeCache = new(StringComparer.Ordinal);

    public DirectOutboxProcessorHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<DirectOutboxProcessorHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var crashBackoff = TimeSpan.FromMilliseconds(50);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var claimer = scope.ServiceProvider.GetRequiredService<IOutboxBatchClaimer>();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var json = scope.ServiceProvider.GetRequiredService<IJsonSerializer>();
                var state = scope.ServiceProvider.GetRequiredService<IOutboxStateStore>();

                var batch = await claimer.ClaimAsync(_batchSize, _lease, ct);

                if (batch.Count == 0)
                {
                    await Task.Delay(_idleDelay, ct);
                    crashBackoff = TimeSpan.FromMilliseconds(50);
                    continue;
                }

                var processed = new List<Guid>(batch.Count);
                var failed = new List<(Guid Id, string Error)>(capacity: Math.Min(batch.Count, 64));

                // Bounded parallelism (optional but usually faster than sequential)
                using var sem = new SemaphoreSlim(_maxInFlight, _maxInFlight);

                var tasks = batch.Select(async msg =>
                {
                    await sem.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        var ev = Deserialize(msg.MessageType, msg.Payload, json);
                        if (ev is null)
                            throw new InvalidOperationException("Domain event deserialization failed.");

                        await mediator.Publish(ev, ct).ConfigureAwait(false);

                        lock (processed) processed.Add(msg.OutboxId);
                    }
                    catch (Exception ex)
                    {
                        lock (failed) failed.Add((msg.OutboxId, ex.Message));
                        _logger.LogWarning(ex, "[OUTBOX-DIRECT] FAIL OutboxId={OutboxId}", msg.OutboxId);
                    }
                    finally
                    {
                        sem.Release();
                    }
                }).ToArray();

                await Task.WhenAll(tasks).ConfigureAwait(false);

                var now = DateTime.UtcNow;

                // ✅ Bulk mark (you already added these methods earlier)
                if (processed.Count > 0)
                    await state.MarkProcessedBulkAsync(processed, now, ct).ConfigureAwait(false);

                if (failed.Count > 0)
                    await state.MarkFailedBulkAsync(failed, now, nextAttemptUtc: null, ct).ConfigureAwait(false);

                _logger.LogDebug(
                    "[OUTBOX-DIRECT] Batch done. Claimed={Claimed} Processed={Processed} Failed={Failed}",
                    batch.Count, processed.Count, failed.Count);

                crashBackoff = TimeSpan.FromMilliseconds(50);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OUTBOX-DIRECT] Loop crashed. WorkerId={WorkerId}", _workerId);
                await Task.Delay(crashBackoff, ct);
                crashBackoff = TimeSpan.FromMilliseconds(Math.Min(crashBackoff.TotalMilliseconds * 2, 2000));
            }
        }
    }

    private static IDomainEvent? Deserialize(string? messageType, string? payload, IJsonSerializer json)
    {
        if (string.IsNullOrWhiteSpace(messageType)) return null;

        var t = TypeCache.GetOrAdd(messageType, static mt => Type.GetType(mt));
        if (t is null) return null;

        return json.DeserializeObject(payload ?? "{}", t) as IDomainEvent;
    }
}
