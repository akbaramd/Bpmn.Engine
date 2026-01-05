using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Repositories;

namespace Novin.Bpmn.Engine.Application.Queries.GetUserTasksInboxQuery;

public sealed class GetUserTasksInboxQueryHandler
    : IRequestHandler<GetUserTasksInboxQuery, PagedResult<UserTaskListItemDto>>
{
    private readonly IUserTaskInstanceRepository _repo;
    private readonly ILogger<GetUserTasksInboxQueryHandler> _logger;

    public GetUserTasksInboxQueryHandler(
        IUserTaskInstanceRepository repo,
        ILogger<GetUserTasksInboxQueryHandler> logger)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<UserTaskListItemDto>> Handle(GetUserTasksInboxQuery request, CancellationToken ct)
    {
        if (request.UserId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(request.UserId));

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 20 : request.PageSize;

        UserTaskStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<UserTaskStatus>(request.Status, ignoreCase: true, out var parsed))
                throw new ArgumentException($"Invalid status '{request.Status}'.", nameof(request.Status));

            statusFilter = parsed;
        }

        _logger.LogInformation(
            "Get inbox tasks UserId={UserId} Status={Status} Page={Page} PageSize={PageSize} Roles={RoleCount}",
            request.UserId,
            request.Status ?? "(any)",
            page,
            pageSize,
            request.Roles?.Count ?? 0);

        var result = await _repo.GetInboxAsync(
            request.UserId,
            request.ProcessId,
            request.Roles ?? Array.Empty<string>(),
            statusFilter,
            page,
            pageSize,
            ct);

        // ✅ Map domain -> DTO HERE
        var dtos = result.Items.Select(t =>
        {
            var assignee = t.GetMeta(UserTaskMeta.Assignee);
            var candidateGroups = SplitCsv(t.GetMeta(UserTaskMeta.CandidateGroups));

            return new UserTaskListItemDto(
                UserTaskId: t.Id,
                Type: t.TaskName, // or t.ElementId if you prefer
                Status: t.Status.ToString(),
                Assignee: string.IsNullOrWhiteSpace(assignee) ? null : assignee,
                CandidateGroups: candidateGroups,
                CreatedAt: new DateTimeOffset(t.CreatedAtUtc, TimeSpan.Zero)
            );
        }).ToList();

        return new PagedResult<UserTaskListItemDto>
        {
            Items = dtos,
            Page = page,
            PageSize = pageSize,
            TotalCount = result.TotalCount
        };
    }

    private static IReadOnlyList<string> SplitCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return Array.Empty<string>();
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Distinct(StringComparer.OrdinalIgnoreCase)
                  .ToList();
    }
}
