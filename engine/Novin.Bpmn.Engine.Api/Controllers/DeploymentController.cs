using Microsoft.AspNetCore.Mvc;
using MediatR;
using Novin.Bpmn.Engine.Application.Commands.DeployProcess;

namespace Novin.Bpmn.Engine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeploymentController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<DeploymentController> _logger;

    public DeploymentController(IMediator mediator, ILogger<DeploymentController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    public async Task<ActionResult<DeployProcessResult>> DeployProcess([FromBody] DeployProcessCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying process");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

