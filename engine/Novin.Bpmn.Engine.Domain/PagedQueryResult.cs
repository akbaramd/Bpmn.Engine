namespace Novin.Bpmn.Engine.Domain;

public sealed class PagedQueryResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required long TotalCount { get; init; }
}