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
            CompleteServiceTaskResult.NotFound => NotFound(new { error = $"Job {workerId} not found" }),
            CompleteServiceTaskResult.InvalidState => Conflict(new { error = "Job is not completable in its current status" }),
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
            FailServiceTaskResult.NotFound => NotFound(new { error = $"Job {workerId} not found" }),
            FailServiceTaskResult.InvalidState => Conflict(new { error = "Job is not fail-able in its current status" }),
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
        string? CandidateUsers,
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
        string Status,
        DateTime CreatedAtUtc,
        DateTime? StartedAtUtc,
        DateTime? CompletedAtUtc,
        IReadOnlyDictionary<string, string> Variables,
        string? ErrorMessage);

    private static WorkerDto Map(Job w) => new(
        WorkerId: w.Id,
        ProcessId: w.ProcessId,
        TokenId: w.TokenId,
        ElementId: w.ElementId,
        TaskName: w.TaskName,
        Status: w.Status.ToString(),
        CreatedAtUtc: w.CreatedAtUtc,
        StartedAtUtc: w.StartedAtUtc,
        CompletedAtUtc: w.CompletedAtUtc,
        Variables: new Dictionary<string, string>(w.Payload),
        ErrorMessage: w.ErrorMessage
    );

    private string GetActor()
        => User?.Identity?.Name
           ?? HttpContext?.User?.FindFirst("sub")?.Value
           ?? "anonymous";
}
