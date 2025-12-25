using MediatR;
using Microsoft.AspNetCore.Mvc;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Queries.GetProcessExecutionFlow;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Api.Controllers;

/// <summary>
/// Controller for process execution flow visualization and audit trails
/// </summary>
[ApiController]
[Route("api/process-execution")]
public sealed class ProcessExecutionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IProcessExecutionRecorder _executionRecorder;

    public ProcessExecutionController(
        IMediator mediator,
        IProcessExecutionRecorder executionRecorder)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _executionRecorder = executionRecorder ?? throw new ArgumentNullException(nameof(executionRecorder));
    }

    /// <summary>
    /// Get the complete execution flow of a process instance for BPMN visualization
    /// </summary>
    /// <param name="processId">The process instance ID</param>
    /// <returns>Complete execution flow data for client-side BPMN visualization</returns>
    [HttpGet("{processId}/flow")]
    [ProducesResponseType(typeof(ProcessExecutionFlowDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ProcessExecutionFlowDto>> GetExecutionFlow(
        Guid processId,
        CancellationToken ct = default)
    {
        var query = new GetProcessExecutionFlowQuery(processId);
        var result = await _mediator.Send(query, ct);

        return Ok(result);
    }

    /// <summary>
    /// Get execution statistics for a process instance
    /// </summary>
    /// <param name="processId">The process instance ID</param>
    /// <returns>Execution statistics summary</returns>
    [HttpGet("{processId}/stats")]
    [ProducesResponseType(typeof(ExecutionStatsDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ExecutionStatsDto>> GetExecutionStats(
        Guid processId,
        CancellationToken ct = default)
    {
        var query = new GetProcessExecutionFlowQuery(processId);
        var result = await _mediator.Send(query, ct);

        return Ok(result.Stats);
    }

    /// <summary>
    /// Get the minimal execution path (audit trail) for a process instance
    /// Contains only executed nodes from start to end events
    /// </summary>
    /// <param name="processId">The process instance ID</param>
    /// <returns>Execution path with minimal node data</returns>
    [HttpGet("{processId}/path")]
    [ProducesResponseType(typeof(IEnumerable<ProcessExecutionNode>), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<IEnumerable<ProcessExecutionNode>>> GetExecutionPath(
        Guid processId,
        CancellationToken ct = default)
    {
        var executionPath = await _executionRecorder.GetExecutionPathAsync(processId, ct);

        if (!executionPath.Any())
        {
            return NotFound(new { error = $"No execution path found for process {processId}" });
        }

        return Ok(executionPath);
    }

    /// <summary>
    /// Get execution statistics from the audit trail
    /// </summary>
    /// <param name="processId">The process instance ID</param>
    /// <returns>Execution statistics from audit trail</returns>
    [HttpGet("{processId}/audit-stats")]
    [ProducesResponseType(typeof(ProcessExecutionStats), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ProcessExecutionStats>> GetAuditStats(
        Guid processId,
        CancellationToken ct = default)
    {
        var stats = await _executionRecorder.GetExecutionStatsAsync(processId, ct);
        return Ok(stats);
    }
}