using Microsoft.AspNetCore.Mvc;
using MediatR;
using Novin.Bpmn.Engine.Application.Commands.CreateNode;
using Novin.Bpmn.Engine.Application.Commands.ProcessNode;
using Novin.Bpmn.Engine.Application.Commands.CompleteNode;

namespace Novin.Bpmn.Engine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NodeController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<NodeController> _logger;

    public NodeController(IMediator mediator, ILogger<NodeController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    public async Task<ActionResult<CreateNodeResult>> CreateNode([FromBody] CreateNodeCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating node");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{nodeId}/process")]
    public async Task<ActionResult<ProcessNodeResult>> ProcessNode(Guid nodeId, [FromBody] Guid? tokenId = null)
    {
        try
        {
            var command = new ProcessNodeCommand(nodeId, tokenId);
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing node");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{nodeId}/complete")]
    public async Task<ActionResult<CompleteNodeResult>> CompleteNode(Guid nodeId, [FromBody] CompleteNodeRequest request)
    {
        try
        {
            var command = new CompleteNodeCommand(nodeId, request.TokenId, request.OutputVariables);
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing node");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public class CompleteNodeRequest
    {
        public Guid? TokenId { get; set; }
        public Dictionary<string, object>? OutputVariables { get; set; }
    }
}

