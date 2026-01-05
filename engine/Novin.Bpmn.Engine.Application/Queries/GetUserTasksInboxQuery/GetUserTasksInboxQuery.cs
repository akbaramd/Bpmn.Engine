using MediatR;

namespace Novin.Bpmn.Engine.Application.Queries.GetUserTasksInboxQuery;

public sealed record GetUserTasksInboxQuery(
    Guid? UserId,
    Guid? ProcessId,
    IReadOnlyCollection<string> Roles,
    string? Status,
    int Page,
    int PageSize
) : IRequest<PagedResult<UserTaskListItemDto>>;
public sealed record UserTaskListItemDto(
    Guid UserTaskId,
    string Type,
    string Status,
    string? Assignee,
    IReadOnlyList<string> CandidateGroups,
    DateTimeOffset CreatedAt
);
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required long TotalCount { get; init; }

    public bool HasNextPage => (long)Page * PageSize < TotalCount;
}
