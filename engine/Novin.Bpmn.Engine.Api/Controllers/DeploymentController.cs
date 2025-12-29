using Microsoft.AspNetCore.Mvc;
using MediatR;
using Novin.Bpmn.Engine.Application.Commands.StartProcess;
using Novin.Bpmn.Engine.Application.Queries.GetDeployment;
using Novin.Bpmn.Engine.Application.Queries.GetDeployments;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

using DeploymentDto = Novin.Bpmn.Engine.Application.Queries.GetDeployment.DeploymentDto;

namespace Novin.Bpmn.Engine.Api.Controllers;

/// <summary>
/// Controller for managing BPMN deployments.
/// ✅ POST creates a deployment ONLY (does NOT start a process).
/// ✅ Starting a process is a separate endpoint: POST /api/deployments/{id}/start
/// </summary>
[ApiController]
[Route("api/deployments")]
public sealed class DeploymentController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<DeploymentController> _logger;

    public DeploymentController(
        IMediator mediator,
        IDeploymentRepository deploymentRepository,
        IUnitOfWork uow,
        ILogger<DeploymentController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _deploymentRepository = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ----------------------------------------------------------------------
    // CREATE DEPLOYMENT (NO START)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Create a deployment (no process instance is started here).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DeploymentDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<DeploymentDto>> CreateDeployment(
        [FromBody] CreateDeploymentRequest request,
        CancellationToken ct = default)
    {
        if (request is null)
            return BadRequest(new { error = "Request body is required." });

        if (request.ProjectId == Guid.Empty)
            return BadRequest(new { error = "ProjectId is required." });

        if (string.IsNullOrWhiteSpace(request.DeploymentKey))
            return BadRequest(new { error = "DeploymentKey is required." });

        if (string.IsNullOrWhiteSpace(request.BpmnXml))
            return BadRequest(new { error = "BpmnXml is required." });

        try
        {
            Deployment? created = null;

            await _uow.ExecuteInTransactionAsync(async trxCt =>
            {
                created = Deployment.Create(
                    projectId: request.ProjectId,
                    deploymentKey: request.DeploymentKey.Trim(),
                    bpmnXml: request.BpmnXml,
                    label: string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim());

                await _deploymentRepository.AddAsync(created, trxCt);

                // IMPORTANT: if your ExecuteInTransactionAsync already SaveChanges, you can remove this.

                _logger.LogInformation(
                    "Deployment created. DeploymentId={DeploymentId} Key={Key} ProjectId={ProjectId}",
                    created.Id, created.DeploymentKey, created.ProjectId);
            }, ct);

            return CreatedAtAction(nameof(GetDeployment), new { id = created!.Id }, MapToDto(created!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating deployment. Key={Key} ProjectId={ProjectId}", request.DeploymentKey, request.ProjectId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ----------------------------------------------------------------------
    // START PROCESS FROM DEPLOYMENT (SEPARATE ENDPOINT)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Start a new process instance using an existing deployment.

    // READ
    // ----------------------------------------------------------------------

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DeploymentDto>), 200)]
    public async Task<ActionResult<IEnumerable<DeploymentDto>>> GetDeployments(
        [FromQuery] string? deploymentKey = null,
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default)
    {
        try
        {
            var query = new GetDeploymentsQuery(deploymentKey, activeOnly);
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting deployments");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DeploymentDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<DeploymentDto>> GetDeployment(Guid id, CancellationToken ct = default)
    {
        try
        {
            var query = new GetDeploymentQuery(id);
            var result = await _mediator.Send(query, ct);

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

    [HttpGet("{id:guid}/xml")]
    [ProducesResponseType(typeof(string), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<string>> GetDeploymentXml(Guid id, CancellationToken ct = default)
    {
        try
        {
            var deployment = await _deploymentRepository.GetByIdAsync(id, ct);
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

    [HttpGet("by-key/{deploymentKey}")]
    [ProducesResponseType(typeof(IEnumerable<DeploymentDto>), 200)]
    public async Task<ActionResult<IEnumerable<DeploymentDto>>> GetDeploymentsByKey(string deploymentKey, CancellationToken ct = default)
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

    // ----------------------------------------------------------------------
    // UPDATE
    // ----------------------------------------------------------------------

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DeploymentDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<DeploymentDto>> UpdateDeployment(
        Guid id,
        [FromBody] UpdateDeploymentRequest request,
        CancellationToken ct = default)
    {
        try
        {
            DeploymentDto? result = null;

            await _uow.ExecuteInTransactionAsync(async trxCt =>
            {
                var deployment = await _deploymentRepository.GetByIdAsync(id, trxCt);
                if (deployment == null)
                    throw new InvalidOperationException($"Deployment {id} not found");

                if (!string.IsNullOrWhiteSpace(request.BpmnXml))
                    deployment.UpdateBpmnXml(request.BpmnXml);

                if (!string.IsNullOrWhiteSpace(request.Label))
                    deployment.UpdateLabel(request.Label.Trim());

                await _deploymentRepository.UpdateAsync(deployment, trxCt);

                result = MapToDto(deployment);
            }, ct);

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

    [HttpPut("{id:guid}/xml")]
    [ProducesResponseType(typeof(DeploymentDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<DeploymentDto>> UpdateDeploymentXml(
        Guid id,
        [FromBody] UpdateDeploymentXmlRequest request,
        CancellationToken ct = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.BpmnXml))
            return BadRequest(new { error = "BpmnXml cannot be empty." });

        try
        {
            DeploymentDto? result = null;

            await _uow.ExecuteInTransactionAsync(async trxCt =>
            {
                var deployment = await _deploymentRepository.GetByIdAsync(id, trxCt);
                if (deployment == null)
                    throw new InvalidOperationException($"Deployment {id} not found");

                deployment.UpdateBpmnXml(request.BpmnXml);

                await _deploymentRepository.UpdateAsync(deployment, trxCt);

                result = MapToDto(deployment);
            }, ct);

            return Ok(result);
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

    // ----------------------------------------------------------------------
    // ACTIVATE / DEACTIVATE / DELETE (SOFT)
    // ----------------------------------------------------------------------

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ActivateDeployment(Guid id, CancellationToken ct = default)
    {
        try
        {
            await _uow.ExecuteInTransactionAsync(async trxCt =>
            {
                var deployment = await _deploymentRepository.GetByIdAsync(id, trxCt);
                if (deployment == null)
                    throw new InvalidOperationException($"Deployment {id} not found");

                deployment.Activate();
                await _deploymentRepository.UpdateAsync(deployment, trxCt);
            }, ct);

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

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeactivateDeployment(Guid id, CancellationToken ct = default)
    {
        try
        {
            await _uow.ExecuteInTransactionAsync(async trxCt =>
            {
                var deployment = await _deploymentRepository.GetByIdAsync(id, trxCt);
                if (deployment == null)
                    throw new InvalidOperationException($"Deployment {id} not found");

                deployment.Deactivate();
                await _deploymentRepository.UpdateAsync(deployment, trxCt);
            }, ct);

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

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteDeployment(Guid id, CancellationToken ct = default)
    {
        try
        {
            await _uow.ExecuteInTransactionAsync(async trxCt =>
            {
                var deployment = await _deploymentRepository.GetByIdAsync(id, trxCt);
                if (deployment == null)
                    throw new InvalidOperationException($"Deployment {id} not found");

                deployment.Deactivate(); // soft delete
                await _deploymentRepository.UpdateAsync(deployment, trxCt);
            }, ct);

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

    // ----------------------------------------------------------------------
    // DTO mapping
    // ----------------------------------------------------------------------

    private static DeploymentDto MapToDto(Deployment deployment)
    {
        var label = string.IsNullOrWhiteSpace(deployment.Label) ? deployment.DeploymentKey : deployment.Label;
        return new DeploymentDto(
            deployment.Id,
            deployment.DeploymentKey,
            label,
            deployment.Version,
            deployment.BpmnXml,
            deployment.DeployedAtUtc,
            deployment.IsActive
        );
    }
}

// ----------------------------------------------------------------------
// Requests
// ----------------------------------------------------------------------

public sealed record CreateDeploymentRequest(
    Guid ProjectId,
    string DeploymentKey,
    string BpmnXml,
    string? Label = null
);

public sealed record StartProcessFromDeploymentRequest(
    string ProcessBpmnId,
    string? BusinessKey = null,
    IDictionary<string, object?>? Variables = null
);

public sealed record UpdateDeploymentRequest(
    string? BpmnXml = null,
    string? Label = null
);

public sealed record UpdateDeploymentXmlRequest(
    string BpmnXml
);
