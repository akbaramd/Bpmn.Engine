using Microsoft.AspNetCore.Mvc;
using MediatR;
using Novin.Bpmn.Engine.Application.Commands.DeployProcess;
using Novin.Bpmn.Engine.Application.Queries.GetDeployment;
using Novin.Bpmn.Engine.Application.Queries.GetDeployments;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using DeploymentDto = Novin.Bpmn.Engine.Application.Queries.GetDeployment.DeploymentDto;

namespace Novin.Bpmn.Engine.Api.Controllers;

/// <summary>
/// Controller for managing BPMN process deployments
/// </summary>
[ApiController]
[Route("api/deployments")]
public class DeploymentController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly ILogger<DeploymentController> _logger;

    public DeploymentController(
        IMediator mediator,
        IDeploymentRepository deploymentRepository,
        ILogger<DeploymentController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _deploymentRepository = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Deploy a new BPMN process
    /// </summary>
    /// <param name="command">Deployment command with BPMN XML and metadata</param>
    /// <returns>Deployment result with deployment ID and version</returns>
    [HttpPost]
    [ProducesResponseType(typeof(DeployProcessResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<DeployProcessResult>> DeployProcess([FromBody] DeployProcessCommand command)
    {
        try
        {
            _logger.LogInformation("Deploying process: {DeploymentKey}", command.DeploymentKey);

            var result = await _mediator.Send(command);

            _logger.LogInformation("Process deployed successfully: {DeploymentId}", result.DeploymentId);

            return CreatedAtAction(nameof(GetDeployment), new { id = result.DeploymentId }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying process: {DeploymentKey}", command.DeploymentKey);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all deployments
    /// </summary>
    /// <param name="deploymentKey">Optional filter by deployment key</param>
    /// <param name="activeOnly">Filter only active deployments</param>
    /// <returns>List of deployments</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DeploymentDto>), 200)]
    public async Task<ActionResult<IEnumerable<DeploymentDto>>> GetDeployments(
        [FromQuery] string? deploymentKey = null,
        [FromQuery] bool activeOnly = false)
    {
        try
        {
            var query = new GetDeploymentsQuery(deploymentKey, activeOnly);
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting deployments");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get deployment by ID
    /// </summary>
    /// <param name="id">Deployment ID</param>
    /// <returns>Deployment details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DeploymentDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<DeploymentDto>> GetDeployment(Guid id)
    {
        try
        {
            var query = new GetDeploymentQuery(id);
            var result = await _mediator.Send(query);

            if (result == null)
                return NotFound(new { error = $"Deployment {id} not found" });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting deployment {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get deployment BPMN XML
    /// </summary>
    /// <param name="id">Deployment ID</param>
    /// <returns>BPMN XML content</returns>
    [HttpGet("{id}/xml")]
    [ProducesResponseType(typeof(string), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<string>> GetDeploymentXml(Guid id)
    {
        try
        {
            var deployment = await _deploymentRepository.GetByIdAsync(id);
            if (deployment == null)
                return NotFound(new { error = $"Deployment {id} not found" });

            return Content(deployment.BpmnXml, "application/xml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting deployment XML for {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get deployments by deployment key
    /// </summary>
    /// <param name="deploymentKey">Deployment key</param>
    /// <returns>List of deployments for the deployment key</returns>
    [HttpGet("by-key/{deploymentKey}")]
    [ProducesResponseType(typeof(IEnumerable<DeploymentDto>), 200)]
    public async Task<ActionResult<IEnumerable<DeploymentDto>>> GetDeploymentsByKey(string deploymentKey)
    {
        try
        {
            var deployments = await _deploymentRepository.GetByDeploymentKeyAndVersionAsync(deploymentKey);
            return Ok(deployments.Select(MapToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting deployments for deployment key {DeploymentKey}", deploymentKey);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a deployment (soft delete - mark as inactive)
    /// </summary>
    /// <param name="id">Deployment ID</param>
    /// <returns>Success status</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteDeployment(Guid id)
    {
        try
        {
            var deployment = await _deploymentRepository.GetByIdAsync(id);
            if (deployment == null)
                return NotFound(new { error = $"Deployment {id} not found" });

            // Mark as inactive instead of hard delete
            deployment.Deactivate();
            await _deploymentRepository.UpdateAsync(deployment);

            _logger.LogInformation("Deployment {Id} marked as inactive", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting deployment {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Activate a deployment
    /// </summary>
    /// <param name="id">Deployment ID</param>
    /// <returns>Success status</returns>
    [HttpPost("{id}/activate")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ActivateDeployment(Guid id)
    {
        try
        {
            var deployment = await _deploymentRepository.GetByIdAsync(id);
            if (deployment == null)
                return NotFound(new { error = $"Deployment {id} not found" });

            deployment.Activate();
            await _deploymentRepository.UpdateAsync(deployment);

            _logger.LogInformation("Deployment {Id} activated", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating deployment {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Deactivate a deployment
    /// </summary>
    /// <param name="id">Deployment ID</param>
    /// <returns>Success status</returns>
    [HttpPost("{id}/deactivate")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeactivateDeployment(Guid id)
    {
        try
        {
            var deployment = await _deploymentRepository.GetByIdAsync(id);
            if (deployment == null)
                return NotFound(new { error = $"Deployment {id} not found" });

            deployment.Deactivate();
            await _deploymentRepository.UpdateAsync(deployment);

            _logger.LogInformation("Deployment {Id} deactivated", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating deployment {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private static DeploymentDto MapToDto(Deployment deployment) => new(
        deployment.Id,
        deployment.DeploymentKey,
        deployment.Label,
        deployment.Version,
        deployment.BpmnXml,
        deployment.DeployedAt,
        deployment.IsActive
    );
}

