public interface IOutboxWorkSignal
{
    ValueTask SignalAsync(CancellationToken ct = default);
    ValueTask WaitAsync(TimeSpan maxWait, CancellationToken ct);
}