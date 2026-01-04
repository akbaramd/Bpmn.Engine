// =====================================
// 2) In-memory implementation (single-node)
// - Coalescing: 1000 signals => wake once
// - Non-blocking SignalAsync (best effort)
// =====================================
using System.Threading.Channels;
using Novin.Bpmn.Engine.Application.Services.Interfaces.Infrastructure;

namespace Novin.Bpmn.Engine.Infrastructure.Signals;

public sealed class InMemoryOutboxWorkSignal : IOutboxWorkSignal
{
    private readonly Channel<byte> _ch = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    public ValueTask SignalAsync(CancellationToken ct = default)
    {
        _ch.Writer.TryWrite(1);
        return ValueTask.CompletedTask;
    }

    public async ValueTask WaitAsync(TimeSpan maxWait, CancellationToken ct)
    {
        if (maxWait <= TimeSpan.Zero) return;

        using var timeout = new CancellationTokenSource(maxWait);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        try
        {
            await _ch.Reader.ReadAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            // timeout or shutdown => ignore
        }
    }
}
