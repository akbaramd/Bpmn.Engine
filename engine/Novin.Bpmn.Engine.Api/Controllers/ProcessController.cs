using Microsoft.AspNetCore.Mvc;
using MediatR;
using Novin.Bpmn.Engine.Application.Commands.StartProcess;
using Novin.Bpmn.Engine.Application.Commands.CompleteProcess;
using Novin.Bpmn.Engine.Application.Queries.GetProcess;
using Novin.Bpmn.Engine.Application.Queries.GetProcesses;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using ProcessDetailDto = Novin.Bpmn.Engine.Application.Queries.GetProcess.ProcessDto;
using ProcessListDto = Novin.Bpmn.Engine.Application.Queries.GetProcesses.ProcessDto;
using System.Text.Json.Nodes;

namespace Novin.Bpmn.Engine.Api.Controllers
{
    /// <summary>
    /// Controller for managing BPMN process instances
    /// </summary>
    [ApiController]
    [Route("api/processes")]
    public class ProcessController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IProcessRepository _processRepository;
        private readonly IDeploymentRepository _deploymentRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ProcessController> _logger;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IBpmnRuntimeContextFactory _bpmnContextFactory;

        public ProcessController(
            IMediator mediator,
            IProcessRepository processRepository,
            IDeploymentRepository deploymentRepository,
            IUnitOfWork unitOfWork,
            ILogger<ProcessController> logger,
            IJsonSerializer jsonSerializer,
            IBpmnRuntimeContextFactory bpmnContextFactory)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _processRepository = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
            _deploymentRepository = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
            _bpmnContextFactory = bpmnContextFactory ?? throw new ArgumentNullException(nameof(bpmnContextFactory));
        }

        /// <summary>
        /// Start a new process instance
        /// </summary>
        [HttpPost("start")]
        [ProducesResponseType(typeof(StartProcessResult), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<StartProcessResult>> StartProcess([FromBody] StartProcessCommand command)
        {
            try
            {
                _logger.LogInformation("Starting process: {ProcessBpmnId} from deployment {DeploymentId}",
                    command.ProcessBpmnId, command.DeploymentId);

                var result = await _mediator.Send(command);

                _logger.LogInformation("Process started: {ProcessId}", result.ProcessId);

                return CreatedAtAction(nameof(GetProcess), new { processId = result.ProcessId }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting process: {ProcessBpmnId} from deployment {DeploymentId}",
                    command.ProcessBpmnId, command.DeploymentId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get all process instances
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ProcessListDto>), 200)]
        public async Task<ActionResult<IEnumerable<ProcessListDto>>> GetProcesses(
            [FromQuery] ProcessState? state = null,
            [FromQuery] Guid? deploymentId = null,
            [FromQuery] string? processBpmnId = null,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50)
        {
            try
            {
                var query = new GetProcessesQuery(state, deploymentId, processBpmnId, skip, take);
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
        [HttpDelete("{processId}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteProcess(Guid processId)
        {
            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async trxCt =>
                {
                    var process = await _processRepository.GetByIdAsync(processId, trxCt);
                    if (process == null)
                        throw new InvalidOperationException($"Process {processId} not found");

                    await _processRepository.DeleteAsync(processId, trxCt);

                    _logger.LogInformation("Process deleted: {ProcessId}", processId);
                }, CancellationToken.None);

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Process not found: {ProcessId}", processId);
                return NotFound(new { error = ex.Message });
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
        [HttpPost("{processId}/suspend")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> SuspendProcess(Guid processId)
        {
            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async trxCt =>
                {
                    var process = await _processRepository.GetByIdAsync(processId, trxCt);
                    if (process == null)
                        throw new InvalidOperationException($"Process {processId} not found");

                    if (process.State != ProcessState.Running)
                        throw new InvalidOperationException($"Cannot suspend process in {process.State} state");

                    process.Fail("Cannot suspend process in {process.State}");
                    await _processRepository.UpdateAsync(process, trxCt);

                    _logger.LogInformation("Process {ProcessId} suspended", processId);
                }, CancellationToken.None);

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Cannot suspend process {ProcessId}", processId);
                return BadRequest(new { error = ex.Message });
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
        [HttpPost("{processId}/resume")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ResumeProcess(Guid processId)
        {
            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async trxCt =>
                {
                    var process = await _processRepository.GetByIdAsync(processId, trxCt);
                    if (process == null)
                        throw new InvalidOperationException($"Process {processId} not found");

                    if (process.State != ProcessState.Suspended)
                        throw new InvalidOperationException($"Cannot resume process in {process.State} state");

                    process.Start();
                    await _processRepository.UpdateAsync(process, trxCt);

                    _logger.LogInformation("Process {ProcessId} resumed", processId);
                }, CancellationToken.None);

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Cannot resume process {ProcessId}", processId);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming process {ProcessId}", processId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get process variables
        /// </summary>
        [HttpGet("{processId}/variables")]
        [ProducesResponseType(typeof(Dictionary<string, string>), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<JsonObject>> GetProcessVariables(Guid processId)
        {
            try
            {
                var process = await _processRepository.GetByIdAsync(processId);
                if (process == null)
                    return NotFound(new { error = $"Process {processId} not found" });

                return Ok(process.VariablesObject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting variables for process {ProcessId}", processId);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
