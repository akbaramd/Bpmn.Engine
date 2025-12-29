using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Api.Controllers;

[ApiController]
[Route("api/workers")]
public sealed class WorkersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<WorkersController> _logger;

    public WorkersController(IMediator mediator, IUnitOfWork uow, ILogger<WorkersController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }


    // ------------------------------------------------------------
    // USER TASK: ASSIGN
    // ------------------------------------------------------------
    [HttpPost("{workerId:guid}/assign")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> AssignUserTask(Guid workerId, [FromBody] AssignUserTaskRequest body, CancellationToken ct = default)
    {
        var cmd = new AssignUserTaskCommand(
            WorkerId: workerId,
            Assignee: body.Assignee,
            CandidateGroups: body.CandidateGroups,
            Priority: body.Priority,
            DueDateUtc: body.DueDateUtc,
            AssignedBy: GetActor()
        );

        var result = await _mediator.Send(cmd, ct);

        return result switch
        {
            AssignUserTaskResult.NotFound => NotFound(new { error = $"Worker {workerId} not found" }),
            AssignUserTaskResult.InvalidState => Conflict(new { error = "Worker is not assignable in its current status" }),
            _ => NoContent()
        };
    }

    // ------------------------------------------------------------
    // USER TASK: COMPLETE (submit form)
    // ------------------------------------------------------------
    [HttpPost("{workerId:guid}/complete")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> CompleteUserTask(Guid workerId, [FromBody] CompleteUserTaskRequest body, CancellationToken ct = default)
    {
        var cmd = new CompleteUserTaskCommand(
            WorkerId: workerId,
            CompletedBy: GetActor(),
            Result: body.Result ?? new Dictionary<string, string>(),
            Comment: body.Comment
        );

        var result = await _mediator.Send(cmd, ct);

        return result switch
        {
            CompleteUserTaskResult.NotFound => NotFound(new { error = $"Worker {workerId} not found" }),
            CompleteUserTaskResult.InvalidState => Conflict(new { error = "Worker is not completable in its current status" }),
            CompleteUserTaskResult.TokenNotWaiting => Conflict(new { error = "Token is not waiting for this worker" }),
            _ => NoContent()
        };
    }

    // ------------------------------------------------------------
    // SERVICE TASK: COMPLETE
    // ------------------------------------------------------------
    [HttpPost("{workerId:guid}/service/complete")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> CompleteServiceTask(Guid workerId, [FromBody] CompleteServiceTaskRequest body, CancellationToken ct = default)
    {
        var cmd = new CompleteServiceTaskCommand(
            WorkerId: workerId,
            CompletedByClientId: body.CompletedByClientId,
            Result: body.Result ?? new Dictionary<string, string>()
        );

        var result = await _mediator.Send(cmd, ct);

        return result switch
        {
            CompleteServiceTaskResult.NotFound => NotFound(new { error = $"Worker {workerId} not found" }),
            CompleteServiceTaskResult.InvalidState => Conflict(new { error = "Worker is not completable in its current status" }),
            CompleteServiceTaskResult.TokenNotWaiting => Conflict(new { error = "Token is not waiting for this worker" }),
            _ => NoContent()
        };
    }

    // ------------------------------------------------------------
    // SERVICE TASK: FAIL
    // ------------------------------------------------------------
    [HttpPost("{workerId:guid}/service/fail")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> FailServiceTask(Guid workerId, [FromBody] FailServiceTaskRequest body, CancellationToken ct = default)
    {
        var cmd = new FailServiceTaskCommand(
            WorkerId: workerId,
            FailedByClientId: body.FailedByClientId,
            ErrorMessage: body.ErrorMessage,
            ErrorCode: body.ErrorCode
        );

        var result = await _mediator.Send(cmd, ct);

        return result switch
        {
            FailServiceTaskResult.NotFound => NotFound(new { error = $"Worker {workerId} not found" }),
            FailServiceTaskResult.InvalidState => Conflict(new { error = "Worker is not fail-able in its current status" }),
            FailServiceTaskResult.TokenNotWaiting => Conflict(new { error = "Token is not waiting for this worker" }),
            _ => NoContent()
        };
    }

    // ------------------------------------------------------------
    // DTOs
    // ------------------------------------------------------------
    public sealed record AssignUserTaskRequest(
        string? Assignee,
        string? CandidateGroups,
        int? Priority,
        DateTime? DueDateUtc);

    public sealed record CompleteUserTaskRequest(
        Dictionary<string, string>? Result,
        string? Comment);

    public sealed record CompleteServiceTaskRequest(
        string CompletedByClientId,
        Dictionary<string, string>? Result);

    public sealed record FailServiceTaskRequest(
        string FailedByClientId,
        string ErrorMessage,
        string? ErrorCode);

    public sealed record WorkerDto(
        Guid WorkerId,
        Guid ProcessId,
        Guid TokenId,
        string ElementId,
        string TaskName,
        string Type,
        string Status,
        DateTime CreatedAtUtc,
        DateTime? StartedAtUtc,
        DateTime? CompletedAtUtc,
        string? CompletedBy,
        IReadOnlyDictionary<string, string> Metadata,
        IReadOnlyDictionary<string, string> Variables,
        string? ErrorMessage);

    private static WorkerDto Map(Job w) => new(
        WorkerId: w.Id,
        ProcessId: w.ProcessId,
        TokenId: w.TokenId,
        ElementId: w.ElementId,
        TaskName: w.TaskName,
        Type: w.Type.ToString(),
        Status: w.Status.ToString(),
        CreatedAtUtc: w.CreatedAtUtc,
        StartedAtUtc: w.StartedAtUtc,
        CompletedAtUtc: w.CompletedAtUtc,
        CompletedBy: w.ActorId,
        Metadata: new Dictionary<string, string>(w.Metadata),
        Variables: new Dictionary<string, string>(w.Variables),
        ErrorMessage: w.ErrorMessage
    );

    private string GetActor()
        => User?.Identity?.Name
           ?? HttpContext?.User?.FindFirst("sub")?.Value
           ?? "anonymous";
}
