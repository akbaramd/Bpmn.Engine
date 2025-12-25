using Microsoft.AspNetCore.Mvc;
using MediatR;
using Novin.Bpmn.Engine.Application.Queries.GetIncidents;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Api.Controllers;

/// <summary>
/// Controller for managing BPMN process incidents
/// </summary>
[ApiController]
[Route("api/incidents")]
public class IncidentController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IIncidentRepository _incidentRepository;
    private readonly ILogger<IncidentController> _logger;

    public IncidentController(
        IMediator mediator,
        IIncidentRepository incidentRepository,
        ILogger<IncidentController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _incidentRepository = incidentRepository ?? throw new ArgumentNullException(nameof(incidentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get all incidents
    /// </summary>
    /// <param name="processId">Filter by process ID</param>
    /// <param name="type">Filter by incident type (BpmnError, TechnicalFailure)</param>
    /// <param name="resolved">Filter by resolution status</param>
    /// <returns>List of incidents</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<IncidentDto>), 200)]
    public async Task<ActionResult<IEnumerable<IncidentDto>>> GetIncidents(
        [FromQuery] Guid? processId = null,
        [FromQuery] string? type = null,
        [FromQuery] bool? resolved = null)
    {
        try
        {
            if (processId.HasValue)
            {
                var query = new GetIncidentsQuery(processId.Value);
                var result = await _mediator.Send(query);
                return Ok(result);
            }

            // Get all incidents with optional filtering
            var allIncidents = await _incidentRepository.GetAllAsync();

            var filtered = allIncidents.AsQueryable();

            if (!string.IsNullOrEmpty(type))
                filtered = filtered.Where(i => i.Type.ToString() == type);

            if (resolved.HasValue)
                filtered = filtered.Where(i => (i.Status == Domain.ValueObjects.IncidentStatus.Resolved) == resolved.Value);

            return Ok(filtered.Select(MapToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting incidents");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get incidents by process ID
    /// </summary>
    /// <param name="processId">Process ID</param>
    /// <returns>List of incidents for the process</returns>
    [HttpGet("process/{processId}")]
    [ProducesResponseType(typeof(IEnumerable<IncidentDto>), 200)]
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

    /// <summary>
    /// Get incident by ID
    /// </summary>
    /// <param name="incidentId">Incident ID</param>
    /// <returns>Incident details</returns>
    [HttpGet("{incidentId}")]
    [ProducesResponseType(typeof(IncidentDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<IncidentDto>> GetIncident(Guid incidentId)
    {
        try
        {
            var incident = await _incidentRepository.GetByIdAsync(incidentId);
            if (incident == null)
                return NotFound(new { error = $"Incident {incidentId} not found" });

            return Ok(MapToDto(incident));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting incident {IncidentId}", incidentId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Resolve an incident
    /// </summary>
    /// <param name="incidentId">Incident ID</param>
    /// <param name="resolution">Resolution details</param>
    /// <returns>Success status</returns>
    [HttpPost("{incidentId}/resolve")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ResolveIncident(
        Guid incidentId,
        [FromBody] IncidentResolutionDto? resolution = null)
    {
        try
        {
            var incident = await _incidentRepository.GetByIdAsync(incidentId);
            if (incident == null)
                return NotFound(new { error = $"Incident {incidentId} not found" });

            if (incident.Status == IncidentStatus.Resolved)
                return BadRequest(new { error = "Incident is already resolved" });

            // Mark as resolved
            incident.Resolve();

            if (resolution != null)
            {
                // TODO: Store resolution details if needed
                _logger.LogInformation("Incident {IncidentId} resolved: {Message}", incidentId, resolution.Message);
            }

            await _incidentRepository.UpdateAsync(incident);

            _logger.LogInformation("Incident resolved: {IncidentId}", incidentId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving incident {IncidentId}", incidentId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete an incident
    /// </summary>
    /// <param name="incidentId">Incident ID</param>
    /// <returns>Success status</returns>
    [HttpDelete("{incidentId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteIncident(Guid incidentId)
    {
        try
        {
            var incident = await _incidentRepository.GetByIdAsync(incidentId);
            if (incident == null)
                return NotFound(new { error = $"Incident {incidentId} not found" });

            await _incidentRepository.DeleteAsync(incident);

            _logger.LogInformation("Incident deleted: {IncidentId}", incidentId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting incident {IncidentId}", incidentId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get incident statistics
    /// </summary>
    /// <returns>Incident statistics</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(IncidentStatsDto), 200)]
    public async Task<ActionResult<IncidentStatsDto>> GetIncidentStats()
    {
        try
        {
            var allIncidents = await _incidentRepository.GetAllAsync();

            var stats = new IncidentStatsDto
            {
                TotalIncidents = allIncidents.Count(),
                ResolvedIncidents = allIncidents.Count(i => i.Status == Domain.ValueObjects.IncidentStatus.Resolved),
                UnresolvedIncidents = allIncidents.Count(i => i.Status == Domain.ValueObjects.IncidentStatus.Open),
                FailedJobIncidents = allIncidents.Count(i => i.Type == Domain.ValueObjects.ErrorType.TechnicalFailure && i.ElementId.Contains("Task")),
                FailedExternalTaskIncidents = allIncidents.Count(i => i.Type == Domain.ValueObjects.ErrorType.TechnicalFailure && i.ElementId.Contains("Service")),
                ConditionIncidents = allIncidents.Count(i => i.Type == Domain.ValueObjects.ErrorType.BpmnError)
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting incident statistics");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private static IncidentDto MapToDto(Incident incident) => new IncidentDto
    {
        Id = incident.Id,
        ProcessId = incident.ProcessId,
        TokenId = incident.TokenId,
        ElementId = incident.ElementId,
        Type = incident.Type.ToString(),
        ErrorCode = incident.ErrorCode,
        Message = incident.Message,
        StackTrace = incident.StackTrace,
        Status = incident.Status.ToString(),
        Retries = incident.Retries,
        CreatedAt = incident.CreatedAt,
        LastOccurredAt = incident.LastOccurredAt,
        ResolvedAt = incident.ResolvedAt
    };
}

/// <summary>
/// Incident resolution DTO
/// </summary>
public record IncidentResolutionDto
{
    public string? Message { get; init; }
    public string? ResolutionDetails { get; init; }
}

/// <summary>
/// Incident statistics DTO
/// </summary>
public record IncidentStatsDto
{
    public int TotalIncidents { get; init; }
    public int ResolvedIncidents { get; init; }
    public int UnresolvedIncidents { get; init; }
    public int FailedJobIncidents { get; init; }
    public int FailedExternalTaskIncidents { get; init; }
    public int ConditionIncidents { get; init; }
}

