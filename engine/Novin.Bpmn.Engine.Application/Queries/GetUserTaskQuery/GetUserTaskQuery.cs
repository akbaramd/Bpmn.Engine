using System.Text.Json.Nodes;
using MediatR;

namespace Novin.Bpmn.Engine.Application.Queries.GetUserTask;

public sealed record GetUserTaskQuery(Guid UserTaskId) : IRequest<UserTaskDto?>;

public sealed class UserTaskDto
{
    public Guid UserTaskId { get; init; }
    public string Status { get; init; } = default!;
    public string? Assignee { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public JsonObject? Variables { get; init; }
}