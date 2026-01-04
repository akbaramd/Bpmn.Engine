
public interface IOutboxStateStore
{
    Task MarkDispatchedAsync(IReadOnlyList<Guid> ids, DateTime dispatchedAtUtc, CancellationToken ct);
    Task MarkProcessedAsync(Guid id, DateTime processedAtUtc, CancellationToken ct);
    Task MarkFailedAsync(Guid id, string error, DateTime? nextAttemptUtc, CancellationToken ct);
    Task MarkProcessedBulkAsync(List<Guid> processed, DateTime now, CancellationToken ct);
    Task MarkFailedBulkAsync(List<(Guid Id, string Error)> failed, DateTime now, object nextAttemptUtc, CancellationToken ct);
}