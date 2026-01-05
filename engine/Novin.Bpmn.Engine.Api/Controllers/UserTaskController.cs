using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Novin.Bpmn.Engine.Application.Queries.GetUserTask;
using Novin.Bpmn.Engine.Application.Queries.GetUserTasksInboxQuery;

namespace Novin.Bpmn.Engine.Api.Controllers;

[ApiController]
[Route("api/user-tasks")]
[Produces("application/json")]
public sealed class UserTasksController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<UserTasksController> _logger;

    public UserTasksController(IMediator mediator, ILogger<UserTasksController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserTaskListItemDto>), 200)]
    public async Task<IActionResult> GetInbox([FromQuery] GetUserTasksInboxRequest request, CancellationToken ct = default)
    {
       

        var rolesFromClaims = GetRolesFromClaims();
        var roles = (request.Roles?.Where(r => !string.IsNullOrWhiteSpace(r)) ?? Array.Empty<string>())
            .Concat(rolesFromClaims)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // page guards
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 20 : request.PageSize;

        var query = new GetUserTasksInboxQuery(
            UserId: request.UserId,
            ProcessId:request.ProcessId,
            Roles: roles,
            Status: request.Status,
            Page: page,
            PageSize: pageSize
        );

        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    private Guid? TryGetUserIdFromClaims()
    {
        var raw =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("user_id");

        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private string[] GetRolesFromClaims()
    {
        return User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
    // ------------------------------------------------------------
    // USER TASK: COMPLETE (submit form)
    // ------------------------------------------------------------
    [HttpPost("{userTaskId:guid}/complete")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 409)]
    public async Task<IActionResult> CompleteUserTask(
        Guid userTaskId,
        [FromBody] CompleteUserTaskRequest body,
        CancellationToken ct = default)
    {
        if (body is null)
            return BadRequest(new { error = "Request body is required" });

        var cmd = new CompleteUserTaskCommand(
            WorkerId: userTaskId,                  // <- keeps app-layer unchanged
            CompletedBy: GetActor(),
            Result: body.Result ?? new Dictionary<string, object?>(),
            Comment: body.Comment
        );

        var result = await _mediator.Send(cmd, ct);

        return result switch
        {
            CompleteUserTaskResult.NotFound =>
                NotFound(new { error = $"UserTask {userTaskId} not found" }),

            CompleteUserTaskResult.InvalidState =>
                Conflict(new { error = "UserTask is not completable in its current status" }),

            CompleteUserTaskResult.TokenNotWaiting =>
                Conflict(new { error = "Token is not waiting for this user task" }),

            _ => NoContent()
        };
    }

    // ------------------------------------------------------------
    // USER TASK: GET (details)
    // ------------------------------------------------------------
    [HttpGet("{userTaskId:guid}")]
    [ProducesResponseType(typeof(UserTaskDto), 200)]
    [ProducesResponseType(typeof(object), 404)]
    public async Task<IActionResult> GetUserTask(Guid userTaskId, CancellationToken ct = default)
    {
        var query = new GetUserTaskQuery(userTaskId);
        var result = await _mediator.Send(query, ct);

        return result is null
            ? NotFound(new { error = $"UserTask {userTaskId} not found" })
            : Ok(result);
    }

    // ------------------------------------------------------------
    // USER TASK: ASSIGN
    // ------------------------------------------------------------
    [HttpPost("{userTaskId:guid}/assign")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 409)]
    public async Task<IActionResult> AssignUserTask(
        Guid userTaskId,
        [FromBody] AssignUserTaskRequest body,
        CancellationToken ct = default)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Assignee))
            return BadRequest(new { error = "Assignee is required" });

        var cmd = new AssignUserTaskCommand(
            UserTaskId: userTaskId,                  // <- keeps app-layer unchanged
            AssignedBy: body.Assignee,
            Assignee: body.Assignee
        );

        var result = await _mediator.Send(cmd, ct);

        return result switch
        {
            AssignUserTaskResult.NotFound =>
                NotFound(new { error = $"UserTask {userTaskId} not found" }),

            AssignUserTaskResult.InvalidState =>
                Conflict(new { error = "UserTask is not assignable in its current status" }),

            _ => NoContent()
        };
    }

    private string GetActor()
    {
        var name = User?.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(name))
            return name;

        if (Request.Headers.TryGetValue("X-Actor", out var actor) &&
            !string.IsNullOrWhiteSpace(actor.ToString()))
        {
            return actor.ToString();
        }

        return "system";
    }
}

// ------------------------------------------------------------
// Request DTOs
// ------------------------------------------------------------
public sealed record CompleteUserTaskRequest(
    Dictionary<string, object?>? Result,
    string? Comment);

public sealed record AssignUserTaskRequest(
    string Assignee);

// ------------------------------------------------------------
// Response DTO
// ------------------------------------------------------------
public sealed record GetUserTasksInboxRequest(
    Guid? UserId,
    Guid? ProcessId,
    string[]? Roles,
    string? Status,
    int Page = 1,
    int PageSize = 20
);
public sealed record UserTaskDto(
    Guid UserTaskId,                 // <- renamed
    string Type,
    string Status,
    string? Assignee,
    DateTimeOffset CreatedAt,
    Dictionary<string, object?>? Payload);
