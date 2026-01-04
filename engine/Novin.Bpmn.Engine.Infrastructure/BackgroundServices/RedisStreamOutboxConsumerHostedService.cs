using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;
using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Infrastructure.Outbox.Redis;

public sealed class RedisStreamOutboxConsumerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOutboxQueue _queue;
    private readonly RedisOutboxQueueOptions _opt;
    private readonly ILogger<RedisStreamOutboxConsumerHostedService> _logger;

    private readonly string _consumerName = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    private static readonly ConcurrentDictionary<string, Type?> TypeCache = new(StringComparer.Ordinal);

    public RedisStreamOutboxConsumerHostedService(
        IServiceScopeFactory scopeFactory,
        IOutboxQueue queue,
        RedisOutboxQueueOptions opt,
        ILogger<RedisStreamOutboxConsumerHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _opt = opt ?? throw new ArgumentNullException(nameof(opt));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = new Task[_opt.Partitions];
        for (var p = 0; p < _opt.Partitions; p++)
        {
            var partition = p;
            tasks[p] = PartitionLoop(partition, stoppingToken); // ✅ no Task.Run
        }
        return Task.WhenAll(tasks);
    }

    private async Task PartitionLoop(int partition, CancellationToken ct)
    {
        var block = TimeSpan.FromMilliseconds(_opt.BlockMs);
        var minIdle = TimeSpan.FromMilliseconds(_opt.PendingMinIdleMs);

        var crashBackoff = TimeSpan.FromMilliseconds(50);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // optional: run auto-claim کمتر (مثلاً فقط وقتی batch خالی بود)
                await _queue.ClaimStuckPendingAsync(partition, _consumerName, minIdle, _opt.ClaimBatchSize, ct);

                var batch = await _queue.ReadBatchAsync(partition, _opt.ReadBatchSize, block, _consumerName, ct);
                if (batch.Count == 0)
                    continue;

                var processed = new List<Guid>(batch.Count);
                var failed = new List<(Guid Id, string Error)>(capacity: Math.Min(batch.Count, 32));

                // ✅ scope per batch (DbContext leak جلوگیری)
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var json = scope.ServiceProvider.GetRequiredService<IJsonSerializer>();
                var state = scope.ServiceProvider.GetRequiredService<IOutboxStateStore>();

                foreach (var env in batch)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        var domainEvent = DeserializeCached(env.Item, json);
                        if (domainEvent is null)
                            throw new InvalidOperationException("Domain event deserialization failed.");

                        await mediator.Publish(domainEvent, ct);

                        processed.Add(env.Item.OutboxId);
                    }
                    catch (Exception ex)
                    {
                        failed.Add((env.Item.OutboxId, ex.Message));
                        _logger.LogWarning(ex,
                            "[OUTBOX-CONSUME] FAIL Partition={Partition} OutboxId={OutboxId}",
                            partition, env.Item.OutboxId);
                    }
                }

                // ✅ bulk status update (یک یا دو درخواست به ES، نه N تا)
                var now = DateTime.UtcNow;
                if (processed.Count > 0)
                    await state.MarkProcessedBulkAsync(processed, now, ct);

                if (failed.Count > 0)
                    await state.MarkFailedBulkAsync(failed, now, nextAttemptUtc: null, ct);

                // ✅ ack بعد از ثبت وضعیت‌ها
                await _queue.AckAsync(partition, batch, ct);

                crashBackoff = TimeSpan.FromMilliseconds(50);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OUTBOX-CONSUME] Loop crashed Partition={Partition}", partition);
                await Task.Delay(crashBackoff, ct);
                crashBackoff = TimeSpan.FromMilliseconds(Math.Min(crashBackoff.TotalMilliseconds * 2, 2000));
            }
        }
    }

    private static IDomainEvent? DeserializeCached(OutboxQueueItem item, IJsonSerializer json)
    {
        if (string.IsNullOrWhiteSpace(item.MessageType)) return null;

        var t = TypeCache.GetOrAdd(item.MessageType, static mt => Type.GetType(mt));
        if (t is null) return null;

        return json.DeserializeObject(item.Payload, t) as IDomainEvent;
    }
}
