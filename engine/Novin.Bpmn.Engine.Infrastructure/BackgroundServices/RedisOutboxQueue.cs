using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;
using StackExchange.Redis;

namespace Novin.Bpmn.Engine.Infrastructure.Outbox.Redis;

public sealed class RedisOutboxQueue : IOutboxQueue
{
    private readonly IConnectionMultiplexer _mux;
    private readonly RedisOutboxQueueOptions _opt;

    // EnsureGroup cache (per stream)
    private readonly ConcurrentDictionary<string, byte> _groupEnsured = new(StringComparer.Ordinal);

    // XAUTOCLAIM cursor per partition (to avoid rescanning from 0-0 each time)
    private readonly ConcurrentDictionary<int, string> _autoClaimCursor = new();

    private static readonly RedisValue F_OutboxId     = "outboxId";
    private static readonly RedisValue F_PartitionKey = "partitionKey";
    private static readonly RedisValue F_MessageType  = "messageType";
    private static readonly RedisValue F_Payload      = "payload";
    private static readonly RedisValue F_MessageName  = "messageName";
    private static readonly RedisValue F_OccurredAt   = "occurredAtUtc";
    private static readonly RedisValue F_Attempts     = "attempts";

    public RedisOutboxQueue(IConnectionMultiplexer mux, RedisOutboxQueueOptions opt)
    {
        _mux = mux ?? throw new ArgumentNullException(nameof(mux));
        _opt = opt ?? throw new ArgumentNullException(nameof(opt));
        if (_opt.Partitions <= 0) _opt.Partitions = 12;
    }

    private IDatabase Db => _mux.GetDatabase();

    private string StreamName(int partition) => $"{_opt.StreamPrefix}:p{partition}";
    private string Group => _opt.ConsumerGroup;

    public async ValueTask EnqueueAsync(OutboxQueueItem item, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var p = PickPartition(item.PartitionKey, _opt.Partitions);
        var stream = StreamName(p);

        await EnsureGroupAsync(stream).ConfigureAwait(false);

        var entries = new[]
        {
            new NameValueEntry(F_OutboxId, item.OutboxId.ToString("D")),
            new NameValueEntry(F_PartitionKey, item.PartitionKey ?? string.Empty),
            new NameValueEntry(F_MessageType, item.MessageType ?? string.Empty),
            new NameValueEntry(F_Payload, item.Payload ?? string.Empty),
            new NameValueEntry(F_MessageName, item.MessageName ?? string.Empty),
            new NameValueEntry(F_OccurredAt, item.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            new NameValueEntry(F_Attempts, item.Attempts.ToString(CultureInfo.InvariantCulture)),
        };

        _ = await Db.StreamAddAsync(stream, entries).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<OutboxQueueEnvelope>> ReadBatchAsync(
        int partition,
        int maxCount,
        TimeSpan block,
        string consumerName,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var stream = StreamName(partition);
        await EnsureGroupAsync(stream).ConfigureAwait(false);

        // 1) اول Pending همین consumer را بخوان (position = "0")
        var pending = await XReadGroupRawAsync(
            stream: stream,
            group: Group,
            consumer: consumerName,
            position: "0",
            count: maxCount,
            blockMs: 0,
            ct: ct).ConfigureAwait(false);

        if (pending.Count > 0)
            return pending;

        // 2) بعد New را با BLOCK واقعی بخوان (position = ">")
        var blockMs = (int)Math.Max(0, block.TotalMilliseconds);
        return await XReadGroupRawAsync(
            stream: stream,
            group: Group,
            consumer: consumerName,
            position: ">",
            count: maxCount,
            blockMs: blockMs,
            ct: ct).ConfigureAwait(false);
    }

    public async ValueTask AckAsync(int partition, IReadOnlyList<OutboxQueueEnvelope> envelopes, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (envelopes.Count == 0) return;

        var stream = StreamName(partition);

        var ids = new RedisValue[envelopes.Count];
        for (var i = 0; i < envelopes.Count; i++)
            ids[i] = envelopes[i].StreamId;

        // فقط XACK (سریع‌تر از XDEL)
        _ = await Db.StreamAcknowledgeAsync(stream, Group, ids).ConfigureAwait(false);

        // اگر می‌خواهی stream کوچک بماند، بهتر از XDEL، XTRIM ~ است (periodic).
        // اینجا عمداً انجام نمی‌دهیم تا latency بالا نرود.
    }

    public async ValueTask ClaimStuckPendingAsync(
        int partition,
        string consumerName,
        TimeSpan minIdleTime,
        int maxCount,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var stream = StreamName(partition);
        await EnsureGroupAsync(stream).ConfigureAwait(false);

        // XAUTOCLAIM <key> <group> <consumer> <min-idle-time> <start> COUNT <n>
        // returns: [nextStartId, [ [id, [field,val...]], ... ], [deletedIds...]]
        var minIdleMs = (long)Math.Max(0, minIdleTime.TotalMilliseconds);

        var start = _autoClaimCursor.GetOrAdd(partition, "0-0");

        // Execute raw
        RedisResult rr = await Db.ExecuteAsync(
            "XAUTOCLAIM",
            new object[]
            {
                stream, Group, consumerName, minIdleMs, start,
                "COUNT", maxCount
            }).ConfigureAwait(false);

        if (rr.IsNull) return;

        // Parse result
        // result[0] = next start id
        // result[1] = messages array
        var top = (RedisResult[])rr!;
        if (top.Length < 2) return;

        var nextStart = top[0].ToString();
        if (!string.IsNullOrWhiteSpace(nextStart))
            _autoClaimCursor[partition] = nextStart!;

        // ما نیاز نداریم اینجا پیام‌ها را برگردانیم؛ فقط claim کافی است.
        // ReadBatchAsync با position="0" آن‌ها را فوراً می‌خواند.
    }

    private async Task EnsureGroupAsync(string stream)
    {
        if (_groupEnsured.ContainsKey(stream))
            return;

        try
        {
            await Db.StreamCreateConsumerGroupAsync(stream, Group, "0-0", createStream: true).ConfigureAwait(false);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase))
        {
            // already exists
        }

        _groupEnsured.TryAdd(stream, 1);
    }

    private async Task<List<OutboxQueueEnvelope>> XReadGroupRawAsync(
        string stream,
        string group,
        string consumer,
        string position,
        int count,
        int blockMs,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // XREADGROUP GROUP <group> <consumer> COUNT <n> BLOCK <ms> STREAMS <stream> <id>
        // Note: if blockMs == 0 => no BLOCK
        var args = new List<object>(16)
        {
            "GROUP", group, consumer,
            "COUNT", count
        };

        if (blockMs > 0)
        {
            args.Add("BLOCK");
            args.Add(blockMs);
        }

        args.Add("STREAMS");
        args.Add(stream);
        args.Add(position);

        RedisResult rr = await Db.ExecuteAsync("XREADGROUP", args.ToArray()).ConfigureAwait(false);
        if (rr.IsNull) return new List<OutboxQueueEnvelope>(0);

        return ParseXReadGroup(rr, stream, group, consumer, partition: ExtractPartitionFromStream(stream));
    }

    private static List<OutboxQueueEnvelope> ParseXReadGroup(
        RedisResult rr,
        string stream,
        string group,
        string consumer,
        int partition)
    {
        // Expected: [ [ streamName, [ [id, [field,val,field,val...]], ... ] ] ]
        var outer = (RedisResult[])rr!;
        if (outer.Length == 0) return new List<OutboxQueueEnvelope>(0);

        // first stream entry
        var streamEntry = (RedisResult[])outer[0]!;
        if (streamEntry.Length < 2) return new List<OutboxQueueEnvelope>(0);

        var messages = (RedisResult[])streamEntry[1]!;
        if (messages.Length == 0) return new List<OutboxQueueEnvelope>(0);

        var list = new List<OutboxQueueEnvelope>(messages.Length);

        for (var i = 0; i < messages.Length; i++)
        {
            var msg = (RedisResult[])messages[i]!;
            if (msg.Length < 2) continue;

            var id = msg[0].ToString();
            if (string.IsNullOrWhiteSpace(id)) continue;

            var fields = (RedisResult[])msg[1]!;
            // fields = [field, value, field, value, ...]
            Guid outboxId = default;
            string pk = "global";
            string mt = "";
            string payload = "";
            string name = "";
            DateTime occurred = DateTime.UtcNow;
            int attempts = 0;

            for (var f = 0; f + 1 < fields.Length; f += 2)
            {
                var key = fields[f].ToString();
                var val = fields[f + 1].ToString() ?? "";

                if (key == "outboxId")
                {
                    Guid.TryParse(val, out outboxId);
                }
                else if (key == "partitionKey")
                {
                    pk = string.IsNullOrWhiteSpace(val) ? "global" : val;
                }
                else if (key == "messageType")
                {
                    mt = val;
                }
                else if (key == "payload")
                {
                    payload = val;
                }
                else if (key == "messageName")
                {
                    name = val;
                }
                else if (key == "occurredAtUtc")
                {
                    occurred = ParseUtc(val);
                }
                else if (key == "attempts")
                {
                    attempts = ParseInt(val);
                }
            }

            if (outboxId == Guid.Empty) continue;

            list.Add(new OutboxQueueEnvelope(
                Partition: partition,
                StreamId: id!,
                Item: new OutboxQueueItem(outboxId, pk, mt, payload, name, occurred, attempts)));
        }

        return list;
    }

    private static int ExtractPartitionFromStream(string stream)
    {
        // stream format: "<prefix>:p{partition}"
        var idx = stream.LastIndexOf(":p", StringComparison.Ordinal);
        if (idx < 0) return 0;
        var s = stream[(idx + 2)..];
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : 0;
    }

    private static int PickPartition(string key, int partitions)
    {
        if (string.IsNullOrWhiteSpace(key)) key = "global";
        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < key.Length; i++)
            {
                hash ^= key[i];
                hash *= 16777619;
            }
            return (int)(hash % (uint)partitions);
        }
    }

    private static DateTime ParseUtc(string? s)
        => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
            ? dt.ToUniversalTime()
            : DateTime.UtcNow;

    private static int ParseInt(string? s)
        => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
}
