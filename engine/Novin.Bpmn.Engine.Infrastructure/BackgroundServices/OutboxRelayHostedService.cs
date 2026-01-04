using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;

namespace Novin.Bpmn.Engine.Infrastructure.Outbox.Redis;

public sealed class OutboxRelayHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOutboxQueue _queue;
    private readonly ILogger<OutboxRelayHostedService> _logger;

    private readonly int _batchSize = 500;
    private readonly TimeSpan _lease = TimeSpan.FromMinutes(2);
    private readonly TimeSpan _idle = TimeSpan.FromMilliseconds(1);

    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public OutboxRelayHostedService(
        IServiceScopeFactory scopeFactory,
        IOutboxQueue queue,
        ILogger<OutboxRelayHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // ---- Phase A: claim from ES (short scope) ----
                IReadOnlyList<OutboxDispatchItem> batch;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var claimer = scope.ServiceProvider.GetRequiredService<IOutboxBatchClaimer>();
                    batch = await claimer.ClaimAsync(_batchSize, _lease, ct);
                }

          

                // ---- Phase B: enqueue to Redis (no scope needed) ----
                var dispatchedIds = new List<Guid>(batch.Count);

                foreach (var msg in batch)
                {
                    ct.ThrowIfCancellationRequested();

                    await _queue.EnqueueAsync(
                        new OutboxQueueItem(
                            OutboxId: msg.OutboxId,
                            PartitionKey: msg.PartitionKey ?? "global",
                            MessageType: msg.MessageType ?? "",
                            Payload: msg.Payload ?? "{}",
                            MessageName: msg.MessageName ?? "",
                            OccurredAtUtc: msg.OccurredAtUtc,
                            Attempts: msg.Attempts),
                        ct);

                    dispatchedIds.Add(msg.OutboxId);
                }

                // ---- Phase C: mark dispatched in ES (short scope) ----
                using (var scope = _scopeFactory.CreateScope())
                {
                    var state = scope.ServiceProvider.GetRequiredService<IOutboxStateStore>();
                    await state.MarkDispatchedAsync(dispatchedIds, DateTime.UtcNow, ct);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxRelay error. WorkerId={WorkerId}", _workerId);
                await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
            }
        }
    }
}
