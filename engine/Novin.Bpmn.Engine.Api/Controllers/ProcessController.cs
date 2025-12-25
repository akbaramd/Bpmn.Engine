using Microsoft.AspNetCore.Mvc;
using MediatR;
using Novin.Bpmn.Engine.Application.Commands.StartProcess;
using Novin.Bpmn.Engine.Application.Commands.CompleteProcess;
using Novin.Bpmn.Engine.Application.Queries.GetProcess;
using Novin.Bpmn.Engine.Application.Queries.GetProcesses;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using ProcessDetailDto = Novin.Bpmn.Engine.Application.Queries.GetProcess.ProcessDto;
using ProcessListDto = Novin.Bpmn.Engine.Application.Queries.GetProcesses.ProcessDto;

namespace Novin.Bpmn.Engine.Api.Controllers;

/// <summary>
/// Controller for managing BPMN process instances
/// </summary>
[ApiController]
[Route("api/processes")]
public class ProcessController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IProcessRepository _processRepository;
    private readonly ILogger<ProcessController> _logger;

    public ProcessController(
        IMediator mediator,
        IProcessRepository processRepository,
        ILogger<ProcessController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _processRepository = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Start a new process instance
    /// </summary>
    /// <param name="command">Process start command with process key and variables</param>
    /// <returns>Process start result with process ID</returns>
    [HttpPost("start")]
    [ProducesResponseType(typeof(StartProcessResult), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<StartProcessResult>> StartProcess([FromBody] StartProcessCommand command)
    {
        try
        {
            _logger.LogInformation("Starting process: {ProcessDefinitionId}", command.ProcessDefinitionId);

            var result = await _mediator.Send(command);

            _logger.LogInformation("Process started: {ProcessId}", result.ProcessId);

            return CreatedAtAction(nameof(GetProcess), new { processId = result.ProcessId }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting process: {ProcessDefinitionId}", command.ProcessDefinitionId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all process instances
    /// </summary>
    /// <param name="state">Filter by process state</param>
    /// <param name="processDefinitionId">Filter by process definition ID</param>
    /// <param name="skip">Number of records to skip</param>
    /// <param name="take">Number of records to take</param>
    /// <returns>List of process instances</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProcessListDto>), 200)]
    public async Task<ActionResult<IEnumerable<ProcessListDto>>> GetProcesses(
        [FromQuery] ProcessState? state = null,
        [FromQuery] string? processDefinitionId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        try
        {
            var query = new GetProcessesQuery(state, processDefinitionId, skip, take);
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting processes");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get process instance by ID
    /// </summary>
    /// <param name="processId">Process instance ID</param>
    /// <returns>Process details</returns>
    [HttpGet("{processId}")]
    [ProducesResponseType(typeof(ProcessDetailDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ProcessDetailDto>> GetProcess(Guid processId)
    {
        try
        {
            var query = new GetProcessQuery(processId);
            var result = await _mediator.Send(query);

            if (result == null)
                return NotFound(new { error = $"Process {processId} not found" });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting process {ProcessId}", processId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Complete a process instance
    /// </summary>
    /// <param name="processId">Process instance ID</param>
    /// <returns>Completion result</returns>
    [HttpPost("{processId}/complete")]
    [ProducesResponseType(typeof(CompleteProcessResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<CompleteProcessResult>> CompleteProcess(Guid processId)
    {
        try
        {
            _logger.LogInformation("Completing process: {ProcessId}", processId);

            var command = new CompleteProcessCommand(processId);
            var result = await _mediator.Send(command);

            _logger.LogInformation("Process completed: {ProcessId}", processId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing process {ProcessId}", processId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a process instance
    /// </summary>
    /// <param name="processId">Process instance ID</param>
    /// <returns>Success status</returns>
    [HttpDelete("{processId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteProcess(Guid processId)
    {
        try
        {
            var process = await _processRepository.GetByIdAsync(processId);
            if (process == null)
                return NotFound(new { error = $"Process {processId} not found" });

            await _processRepository.DeleteAsync(processId);

            _logger.LogInformation("Process deleted: {ProcessId}", processId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting process {ProcessId}", processId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Suspend a running process
    /// </summary>
    /// <param name="processId">Process instance ID</param>
    /// <returns>Success status</returns>
    [HttpPost("{processId}/suspend")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SuspendProcess(Guid processId)
    {
        try
        {
            var process = await _processRepository.GetByIdAsync(processId);
            if (process == null)
                return NotFound(new { error = $"Process {processId} not found" });

            if (process.State != ProcessState.Running)
                return BadRequest(new { error = $"Cannot suspend process in {process.State} state" });

            // TODO: Implement suspend logic
            _logger.LogWarning("Process suspension not yet implemented: {ProcessId}", processId);

            return StatusCode(501, new { error = "Process suspension not yet implemented" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending process {ProcessId}", processId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Resume a suspended process
    /// </summary>
    /// <param name="processId">Process instance ID</param>
    /// <returns>Success status</returns>
    [HttpPost("{processId}/resume")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ResumeProcess(Guid processId)
    {
        try
        {
            var process = await _processRepository.GetByIdAsync(processId);
            if (process == null)
                return NotFound(new { error = $"Process {processId} not found" });

            // TODO: Implement resume logic
            _logger.LogWarning("Process resume not yet implemented: {ProcessId}", processId);

            return StatusCode(501, new { error = "Process resume not yet implemented" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming process {ProcessId}", processId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Send a message to a process instance
    /// </summary>
    /// <param name="processId">Process instance ID</param>
    /// <param name="messageName">Message name</param>
    /// <param name="variables">Message variables</param>
    /// <returns>Success status</returns>
    [HttpPost("{processId}/message")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SendMessage(
        Guid processId,
        [FromQuery] string messageName,
        [FromBody] Dictionary<string, object>? variables = null)
    {
        try
        {
            _logger.LogInformation("Sending message '{MessageName}' to process {ProcessId}", messageName, processId);

            // TODO: Implement message correlation logic
            _logger.LogWarning("Message sending not yet implemented: {ProcessId}, {MessageName}", processId, messageName);

            return StatusCode(501, new { error = "Message sending not yet implemented" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to process {ProcessId}", processId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Send a signal to process instances
    /// </summary>
    /// <param name="signalName">Signal name</param>
    /// <param name="variables">Signal variables</param>
    /// <returns>Success status</returns>
    [HttpPost("signal")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> SendSignal(
        [FromQuery] string signalName,
        [FromBody] Dictionary<string, object>? variables = null)
    {
        try
        {
            _logger.LogInformation("Sending signal '{SignalName}' to all processes", signalName);

            // TODO: Implement signal broadcasting logic
            _logger.LogWarning("Signal sending not yet implemented: {SignalName}", signalName);

            return StatusCode(501, new { error = "Signal sending not yet implemented" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending signal {SignalName}", signalName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get process variables
    /// </summary>
    /// <param name="processId">Process instance ID</param>
    /// <returns>Process variables</returns>
    [HttpGet("{processId}/variables")]
    [ProducesResponseType(typeof(Dictionary<string, object>), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<Dictionary<string, object>>> GetProcessVariables(Guid processId)
    {
        try
        {
            var process = await _processRepository.GetByIdAsync(processId);
            if (process == null)
                return NotFound(new { error = $"Process {processId} not found" });

            return Ok(process.Variables);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting variables for process {ProcessId}", processId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Set process variables
    /// </summary>
    /// <param name="processId">Process instance ID</param>
    /// <param name="variables">Variables to set</param>
    /// <returns>Success status</returns>
    [HttpPut("{processId}/variables")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SetProcessVariables(
        Guid processId,
        [FromBody] Dictionary<string, object> variables)
    {
        try
        {
            var process = await _processRepository.GetByIdAsync(processId);
            if (process == null)
                return NotFound(new { error = $"Process {processId} not found" });

            foreach (var kvp in variables)
            {
                process.SetVariable(kvp.Key, kvp.Value);
            }

            await _processRepository.UpdateAsync(process);

            _logger.LogInformation("Variables updated for process {ProcessId}", processId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting variables for process {ProcessId}", processId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get process statistics
    /// </summary>
    /// <returns>Process statistics</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ProcessStatsDto), 200)]
    public async Task<ActionResult<ProcessStatsDto>> GetProcessStats()
    {
        try
        {
            // Get all processes and calculate stats
            var allProcesses = await _processRepository.GetAllAsync();

            var stats = new ProcessStatsDto
            {
                TotalProcesses = allProcesses.Count(),
                RunningProcesses = allProcesses.Count(p => p.State == ProcessState.Running),
                CompletedProcesses = allProcesses.Count(p => p.State == ProcessState.Completed),
                FailedProcesses = allProcesses.Count(p => p.State == ProcessState.Failed),
                CreatedProcesses = allProcesses.Count(p => p.State == ProcessState.Created)
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting process statistics");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

/// <summary>
/// Process statistics DTO
/// </summary>
public record ProcessStatsDto
{
    public int TotalProcesses { get; init; }
    public int RunningProcesses { get; init; }
    public int CompletedProcesses { get; init; }
    public int FailedProcesses { get; init; }
    public int CreatedProcesses { get; init; }
}
