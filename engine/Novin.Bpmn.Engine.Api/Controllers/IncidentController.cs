using Microsoft.AspNetCore.Mvc;
using MediatR;
using Novin.Bpmn.Engine.Application.Queries.GetIncidents;

namespace Novin.Bpmn.Engine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IncidentController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<IncidentController> _logger;

    public IncidentController(IMediator mediator, ILogger<IncidentController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// دریافت تمام Incident های یک Process
    /// </summary>
    [HttpGet("process/{processId}")]
    public async Task<ActionResult<IEnumerable<IncidentDto>>> GetIncidentsByProcessId(Guid processId)
    {
        try
        {
            var query = new GetIncidentsQuery(processId);
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting incidents for process {ProcessId}", processId);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

