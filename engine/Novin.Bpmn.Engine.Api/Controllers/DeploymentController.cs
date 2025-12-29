using Microsoft.AspNetCore.Mvc;
using MediatR;
using Novin.Bpmn.Engine.Application.Commands.CreateProcessInstance;
using Novin.Bpmn.Engine.Application.Commands.StartProcess;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeploymentController> _logger;

    public DeploymentController(
        IMediator mediator,
        IDeploymentRepository deploymentRepository,
        IUnitOfWork unitOfWork,
        ILogger<DeploymentController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _deploymentRepository = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Create and start a process instance for an existing deployment.
    /// </summary>
    /// <returns>Start result with process ID.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(StartProcessResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<StartProcessResult>> CreateAndStartProcess([FromBody] CreateAndStartRequest request)
    {
        if (request == null)
            return BadRequest(new { error = "Request body is required." });

        try
        {
            _logger.LogInformation("Creating process instance for deployment {DeploymentId} process {ProcessBpmnId}", request.DeploymentId, request.ProcessBpmnId);

            var createCommand = new CreateProcessInstanceCommand(
                request.DeploymentId,
                request.ProcessBpmnId,
                request.ProcessName,
                request.InitialVariables,
                request.BusinessKey);

            var createResult = await _mediator.Send(createCommand);

            var startCommand = new StartProcessCommand(createResult.ProcessId);
            var startResult = await _mediator.Send(startCommand);

            _logger.LogInformation("Process instance created and started. ProcessId={ProcessId}", startResult.ProcessId);

            return CreatedAtAction(nameof(GetDeployment), new { id = request.DeploymentId }, startResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/starting process for deployment {DeploymentId}", request.DeploymentId);
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
            await _unitOfWork.ExecuteInTransactionAsync(async trxCt =>
            {
                var deployment = await _deploymentRepository.GetByIdAsync(id, trxCt);
                if (deployment == null)
                    throw new InvalidOperationException($"Deployment {id} not found");

                // Mark as inactive instead of hard delete
                deployment.Deactivate();
                await _deploymentRepository.UpdateAsync(deployment, trxCt);

                _logger.LogInformation("Deployment {Id} marked as inactive", id);
            }, CancellationToken.None);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Deployment not found: {Id}", id);
            return NotFound(new { error = ex.Message });
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
    public async Task<IActionResult> ActivateDeployment(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async trxCt =>
            {
                var deployment = await _deploymentRepository.GetByIdAsync(id, trxCt);
                if (deployment == null)
                    throw new InvalidOperationException($"Deployment {id} not found");

                deployment.Activate();
                await _deploymentRepository.UpdateAsync(deployment, trxCt);

                _logger.LogInformation("Deployment {Id} activated", id);
            }, cancellationToken);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Deployment not found: {Id}", id);
            return NotFound(new { error = ex.Message });
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
    public async Task<IActionResult> DeactivateDeployment(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async trxCt =>
            {
                var deployment = await _deploymentRepository.GetByIdAsync(id, trxCt);
                if (deployment == null)
                    throw new InvalidOperationException($"Deployment {id} not found");

                deployment.Deactivate();
                await _deploymentRepository.UpdateAsync(deployment, trxCt);

                _logger.LogInformation("Deployment {Id} deactivated", id);
            }, cancellationToken);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Deployment not found: {Id}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating deployment {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing deployment
    /// </summary>
    /// <param name="id">Deployment ID</param>
    /// <param name="request">Update request</param>
    /// <returns>Updated deployment information</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(DeploymentDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<DeploymentDto>> UpdateDeployment(
        Guid id,
        [FromBody] UpdateDeploymentRequest request)
    {
        try
        {
            DeploymentDto? result = null;
            await _unitOfWork.ExecuteInTransactionAsync(async trxCt =>
            {
                var deployment = await _deploymentRepository.GetByIdAsync(id, trxCt);
                if (deployment == null)
                {
                    throw new InvalidOperationException($"Deployment {id} not found");
                }

                // Update allowed fields
                if (!string.IsNullOrWhiteSpace(request.BpmnXml))
                {
                    deployment.UpdateBpmnXml(request.BpmnXml);
                }

                if (!string.IsNullOrWhiteSpace(request.Label))
                {
                    deployment.UpdateLabel(request.Label);
                }

                await _deploymentRepository.UpdateAsync(deployment, trxCt);
                result = MapToDto(deployment);
            }, CancellationToken.None);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Deployment not found: {Id}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating deployment {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update the BPMN XML definition of a deployment
    /// </summary>
    /// <param name="id">Deployment ID</param>
    /// <param name="request">XML update request</param>
    /// <returns>Updated deployment information</returns>
    [HttpPut("{id}/xml")]
    [ProducesResponseType(typeof(DeploymentDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<DeploymentDto>> UpdateDeploymentXml(
        Guid id,
        [FromBody] UpdateDeploymentXmlRequest request)
    {
        try
        {
            DeploymentDto? result = null;
            await _unitOfWork.ExecuteInTransactionAsync(async trxCt =>
            {
                var deployment = await _deploymentRepository.GetByIdAsync(id, trxCt);
                if (deployment == null)
                {
                    throw new InvalidOperationException($"Deployment {id} not found");
                }

                if (string.IsNullOrWhiteSpace(request.BpmnXml))
                {
                    throw new ArgumentException("BPMN XML cannot be null or empty");
                }

                deployment.UpdateBpmnXml(request.BpmnXml);

                await _deploymentRepository.UpdateAsync(deployment, trxCt);
                result = MapToDto(deployment);
            }, CancellationToken.None);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid BPMN XML for deployment {Id}", id);
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Deployment not found: {Id}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating deployment XML {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private static DeploymentDto MapToDto(Deployment deployment)
    {
        var label = string.IsNullOrWhiteSpace(deployment.Label) ? deployment.DeploymentKey : deployment.Label;
        return new DeploymentDto(
        deployment.Id,
        deployment.DeploymentKey,
            label,
        deployment.Version,
        deployment.BpmnXml,
        deployment.DeployedAt,
        deployment.IsActive
    );
}
}

public sealed record CreateAndStartRequest(
    Guid DeploymentId,
    string ProcessBpmnId,
    string ProcessName,
    Dictionary<string, object?>? InitialVariables = null,
    string? BusinessKey = null);

/// <summary>
/// Request to update a deployment
/// </summary>
public record UpdateDeploymentRequest(
    string? BpmnXml = null,
    string? Label = null
);

/// <summary>
/// Request to update deployment XML
/// </summary>
public record UpdateDeploymentXmlRequest(
    string BpmnXml
);
