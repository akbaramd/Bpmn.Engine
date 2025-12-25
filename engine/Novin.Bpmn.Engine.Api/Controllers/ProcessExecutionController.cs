using MediatR;
using Microsoft.AspNetCore.Mvc;
using Novin.Bpmn.Engine.Application.Queries.GetProcessExecutionFlow;

namespace Novin.Bpmn.Engine.Api.Controllers;

/// <summary>
/// Controller for process execution flow visualization
/// </summary>
[ApiController]
[Route("api/process-execution")]
public sealed class ProcessExecutionController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProcessExecutionController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
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
}