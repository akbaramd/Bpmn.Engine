using Microsoft.AspNetCore.Mvc;
using MediatR;
using Novin.Bpmn.Engine.Application.Commands.StartProcess;
using Novin.Bpmn.Engine.Application.Commands.CompleteProcess;
using Novin.Bpmn.Engine.Application.Queries.GetProcess;

namespace Novin.Bpmn.Engine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProcessController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ProcessController> _logger;

    public ProcessController(IMediator mediator, ILogger<ProcessController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("start")]
    public async Task<ActionResult<StartProcessResult>> StartProcess([FromBody] StartProcessCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting process");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{processId}/complete")]
    public async Task<ActionResult<CompleteProcessResult>> CompleteProcess(Guid processId)
    {
        try
        {
            var command = new CompleteProcessCommand(processId);
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing process");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{processId}")]
    public async Task<ActionResult<ProcessDto>> GetProcess(Guid processId)
    {
        try
        {
            var query = new GetProcessQuery(processId);
            var result = await _mediator.Send(query);
            
            if (result == null)
                return NotFound();
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting process");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

