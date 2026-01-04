// Domain/Entities/ExecutionFlowRecord.cs
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Entities;

/// <summary>
/// Immutable execution trace row for visualization/auditing.
/// Append-only: store every TokenMovedEvent as a record.
/// </summary>
public sealed class ExecutionFlowRecord : BaseAggregateRoot
{
    public long Position { get; private set; }

    public Guid ProcessId { get; private set; }
    public Guid TokenId { get; private set; }

    public string FromElementId { get; private set; } = default!;
    public string ToElementId { get; private set; } = default!;

    public DateTime OccurredAtUtc { get; private set; }

    public Guid? ScopeId { get; private set; }
    public Guid? ActivityInstanceId { get; private set; }

    /// <summary>
    /// Idempotency key (unique) - protects against outbox retry duplicates.
    /// 64 hex chars (SHA256)
    /// </summary>
    public string EventKey { get; private set; } = default!;

    // JSON mapped
    private readonly List<string> _viaFlowIds = new();
    public IReadOnlyList<string> ViaFlowIds => _viaFlowIds.AsReadOnly();

    private ExecutionFlowRecord() { }

    public static ExecutionFlowRecord Create(
        Guid processId,
        Guid tokenId,
        long position,
        string fromElementId,
        string toElementId,
        IEnumerable<string>? viaFlowIds,
        DateTime occurredAtUtc,
        Guid? scopeId,
        Guid? activityInstanceId)
    {
        if (processId == Guid.Empty) throw new ArgumentException("ProcessId empty", nameof(processId));
        if (tokenId == Guid.Empty) throw new ArgumentException("TokenId empty", nameof(tokenId));
        if (position <= 0) throw new ArgumentOutOfRangeException(nameof(position), "Position must be > 0");
        if (string.IsNullOrWhiteSpace(fromElementId)) throw new ArgumentException("FromElementId required", nameof(fromElementId));
        if (string.IsNullOrWhiteSpace(toElementId)) throw new ArgumentException("ToElementId required", nameof(toElementId));

        var ts = occurredAtUtc == default ? DateTime.UtcNow : occurredAtUtc;

        var normalized = new List<string>(capacity: 4);
        if (viaFlowIds != null)
        {
            foreach (var f in viaFlowIds)
                if (!string.IsNullOrWhiteSpace(f))
                    normalized.Add(f.Trim());
        }

        var key = BuildEventKey(
            processId: processId,
            tokenId: tokenId,
            fromElementId: fromElementId.Trim(),
            toElementId: toElementId.Trim(),
            viaFlowIds: normalized,
            occurredAtUtcUtc: ts,
            scopeId: scopeId,
            activityInstanceId: activityInstanceId);

        var r = new ExecutionFlowRecord
        {
            ProcessId = processId,
            TokenId = tokenId,
            Position = position,
            FromElementId = fromElementId.Trim(),
            ToElementId = toElementId.Trim(),
            OccurredAtUtc = ts,
            ScopeId = scopeId,
            ActivityInstanceId = activityInstanceId,
            EventKey = key
        };

        if (normalized.Count > 0)
            r._viaFlowIds.AddRange(normalized);

        return r;
    }

    public static string BuildEventKey(
        Guid processId,
        Guid tokenId,
        string fromElementId,
        string toElementId,
        IReadOnlyList<string>? viaFlowIds,
        DateTime occurredAtUtcUtc,
        Guid? scopeId,
        Guid? activityInstanceId)
    {
        var sb = new StringBuilder(256);
        sb.Append(processId).Append('|')
          .Append(tokenId).Append('|')
          .Append(fromElementId).Append('|')
          .Append(toElementId).Append('|')
          .Append(occurredAtUtcUtc.Ticks).Append('|')
          .Append(scopeId?.ToString() ?? "").Append('|')
          .Append(activityInstanceId?.ToString() ?? "").Append('|');

        if (viaFlowIds != null && viaFlowIds.Count > 0)
        {
            for (var i = 0; i < viaFlowIds.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(viaFlowIds[i]);
            }
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);

        var hex = new char[hash.Length * 2];
        var j = 0;
        for (var i = 0; i < hash.Length; i++)
        {
            var b = hash[i];
            hex[j++] = GetHexNibble(b >> 4);
            hex[j++] = GetHexNibble(b & 0xF);
        }
        return new string(hex);
    }

    private static char GetHexNibble(int v)
        => (char)(v < 10 ? ('0' + v) : ('a' + (v - 10)));
}
