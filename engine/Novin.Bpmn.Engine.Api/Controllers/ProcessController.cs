using Microsoft.AspNetCore.Mvc;
using MediatR;
using Novin.Bpmn.Engine.Application.Commands.StartProcess;
using Novin.Bpmn.Engine.Application.Commands.CompleteProcess;
using Novin.Bpmn.Engine.Application.Queries.GetProcess;
using Novin.Bpmn.Engine.Application.Queries.GetProcesses;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Models.Models;
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
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessController> _logger;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly IBpmnRuntimeContextFactory _bpmnContextFactory;
    private readonly IProcessExecutionRecorder _executionRecorder;

    public ProcessController(
        IMediator mediator,
        IProcessRepository processRepository,
        IDeploymentRepository deploymentRepository,
        IUnitOfWork unitOfWork,
        ILogger<ProcessController> logger,
        IJsonSerializer jsonSerializer,
        IBpmnRuntimeContextFactory bpmnContextFactory,
        IProcessExecutionRecorder executionRecorder)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _processRepository = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
        _deploymentRepository = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
        _bpmnContextFactory = bpmnContextFactory ?? throw new ArgumentNullException(nameof(bpmnContextFactory));
        _executionRecorder = executionRecorder ?? throw new ArgumentNullException(nameof(executionRecorder));
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
    /// <param name="state">Filter by process state</param>
    /// <param name="deploymentId">Filter by deployment ID</param>
    /// <param name="processBpmnId">Filter by process BPMN ID</param>
    /// <param name="skip">Number of records to skip</param>
    /// <param name="take">Number of records to take</param>
    /// <returns>List of process instances</returns>
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
            await _unitOfWork.ExecuteInTransactionAsync(async trxCt =>
            {
                var process = await _processRepository.GetByIdAsync(processId, trxCt);
                if (process == null)
                    throw new InvalidOperationException($"Process {processId} not found");

                if (process.State != ProcessState.Running)
                    throw new InvalidOperationException($"Cannot suspend process in {process.State} state");

                process.Suspend();
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
            await _unitOfWork.ExecuteInTransactionAsync(async trxCt =>
            {
                var process = await _processRepository.GetByIdAsync(processId, trxCt);
                if (process == null)
                    throw new InvalidOperationException($"Process {processId} not found");

                if (process.State != ProcessState.Suspended)
                    throw new InvalidOperationException($"Cannot resume process in {process.State} state");

                process.Resume();
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
        [FromBody] Dictionary<string, string>? variables = null)
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
        [FromBody] Dictionary<string, string>? variables = null)
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
    [ProducesResponseType(typeof(Dictionary<string, string>), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<Dictionary<string, string>>> GetProcessVariables(Guid processId)
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
        [FromBody] Dictionary<string, string> variables)
    {
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async trxCt =>
            {
                var process = await _processRepository.GetByIdAsync(processId, trxCt);
                if (process == null)
                    throw new InvalidOperationException($"Process {processId} not found");

                foreach (var kvp in variables)
                {
                    process.SetVariable(kvp.Key, kvp.Value);
                }

                await _processRepository.UpdateAsync(process, trxCt);

                _logger.LogInformation("Variables updated for process {ProcessId}", processId);
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
            _logger.LogError(ex, "Error setting variables for process {ProcessId}", processId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get processes with BPMN model information
    /// </summary>
    /// <param name="state">Filter by process state</param>
    /// <param name="includeModel">Whether to include BPMN model JSON</param>
    /// <param name="skip">Number of records to skip</param>
    /// <param name="take">Number of records to take</param>
    /// <returns>Processes with model information</returns>
    [HttpGet("with-models")]
    [ProducesResponseType(typeof(IEnumerable<ProcessWithModelDto>), 200)]
    public async Task<ActionResult<IEnumerable<ProcessWithModelDto>>> GetProcessesWithModels(
        [FromQuery] ProcessState? state = null,
        [FromQuery] bool includeModel = false,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 10)
    {
        try
        {
            var processes = await _processRepository.GetAllAsync();
            var deployments = await _deploymentRepository.GetActiveDeploymentsAsync();

            // Apply filters
            if (state.HasValue)
            {
                processes = processes.Where(p => p.State == state.Value);
            }

            var result = processes
                .OrderByDescending(p => p.CreatedAt)
                .Skip(skip)
                .Take(take)
                .Select(p =>
                {
                    var deployment = deployments.FirstOrDefault(d => d.Id == p.DeploymentId);
                    string? modelJson = null;

                    if (includeModel && deployment != null)
                    {
                        try
                        {
                            var definitions = deployment.GetDefinitions();
                            modelJson = _jsonSerializer.SerializeObject(definitions);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse BPMN model for deployment {DeploymentId}", deployment.Id);
                        }
                    }

                    return new ProcessWithModelDto
                    {
                        Process = new ProcessDetailDto
                        {
                            Id = p.Id,
                            Name = p.Name,
                            DeploymentId = p.DeploymentId,
                            ProcessBpmnId = p.ProcessBpmnId,
                            State = p.State,
                            Variables = new Dictionary<string, string>(p.Variables),
                            CreatedAt = p.CreatedAt,
                            StartedAt = p.StartedAt,
                            CompletedAt = p.CompletedAt
                        },
                        DeploymentKey = deployment?.DeploymentKey,
                        DeploymentVersion = deployment?.Version,
                        BpmnModel = modelJson,
                        HasModel = modelJson != null
                    };
                });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting processes with models");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get processes by deployment ID
    /// </summary>
    /// <param name="deploymentId">Deployment ID</param>
    /// <param name="state">Filter by process state</param>
    /// <param name="processBpmnId">Filter by process BPMN ID</param>
    /// <param name="skip">Number of records to skip</param>
    /// <param name="take">Number of records to take</param>
    /// <returns>List of processes for the deployment</returns>
    [HttpGet("by-deployment/{deploymentId}")]
    [ProducesResponseType(typeof(IEnumerable<ProcessListDto>), 200)]
    public async Task<ActionResult<IEnumerable<ProcessListDto>>> GetProcessesByDeployment(
        Guid deploymentId,
        [FromQuery] ProcessState? state = null,
        [FromQuery] string? processBpmnId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        try
        {
            // Verify deployment exists
            var deployment = await _deploymentRepository.GetByIdAsync(deploymentId);
            if (deployment == null)
            {
                return NotFound(new { error = $"Deployment {deploymentId} not found" });
            }

            var query = new GetProcessesQuery(
                State: state,
                DeploymentId: deploymentId,
                ProcessBpmnId: processBpmnId,
                Skip: skip,
                Take: take);

            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting processes for deployment {DeploymentId}", deploymentId);
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

    /// <summary>
    /// Get BPMN XML for a process instance
    /// </summary>
    /// <param name="processId">Process instance ID</param>
    /// <returns>BPMN XML content</returns>
    [HttpGet("{processId}/xml")]
    [ProducesResponseType(typeof(string), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<string>> GetProcessXml(Guid processId)
    {
        try
        {
            var process = await _processRepository.GetByIdAsync(processId);
            if (process == null)
            {
                return NotFound(new { error = $"Process {processId} not found" });
            }

            var deployment = await _deploymentRepository.GetByIdAsync(process.DeploymentId);
            if (deployment == null)
            {
                return NotFound(new { error = $"Deployment {process.DeploymentId} not found for process {processId}" });
            }

            _logger.LogInformation("Returning BPMN XML for process {ProcessId} from deployment {DeploymentId}",
                processId, deployment.Id);

            return Content(deployment.BpmnXml, "application/xml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting BPMN XML for process {ProcessId}", processId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get execution history for a process instance
    /// Returns a timeline of all events: tokens, execution nodes, state changes
    /// </summary>
    /// <param name="processId">Process instance ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Process execution history timeline</returns>
    [HttpGet("{processId}/history")]
    [ProducesResponseType(typeof(ProcessHistoryDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ProcessHistoryDto>> GetProcessHistory(
        Guid processId,
        CancellationToken ct = default)
    {
        try
        {
            var process = await _processRepository.GetByIdAsync(processId);
            if (process == null)
            {
                return NotFound(new { error = $"Process {processId} not found" });
            }

            // Get all tokens for this process
            var tokens = await _unitOfWork.Tokens.GetByProcessIdAsync(processId, ct);
            var tokensList = tokens.ToList();

            // Get execution nodes (audit trail) from recorder - these have element name and type in database
            // Use recorder instead of process.ExecutionNodes because it loads from database properly
            var executionNodes = (await _executionRecorder.GetExecutionPathAsync(processId, ct))
                .OrderBy(n => n.ExecutedAt)
                .ToList();

            // Create a lookup dictionary for execution nodes by element ID (for fast access)
            var executionNodeLookup = executionNodes
                .GroupBy(n => n.NodeId)
                .ToDictionary(g => g.Key, g => g.First());

            // Create BPMN runtime context for element information (fallback if execution node doesn't have data)
            BpmnRuntimeContext? bpmnContext = null;
            try
            {
                bpmnContext = await _bpmnContextFactory.CreateAsync(process, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create BPMN context for process {ProcessId}, element details may be missing", processId);
            }

            // Build timeline events
            var historyEvents = new List<HistoryEventDto>();

            // 1. Process creation
            historyEvents.Add(new HistoryEventDto
            {
                EventType = "ProcessCreated",
                Timestamp = process.CreatedAt,
                Description = $"Process '{process.Name}' created",
                ProcessId = process.Id,
                ProcessState = process.State.ToString()
            });

            // 2. Process started
            if (process.StartedAt.HasValue)
            {
                historyEvents.Add(new HistoryEventDto
                {
                    EventType = "ProcessStarted",
                    Timestamp = process.StartedAt.Value,
                    Description = $"Process started",
                    ProcessId = process.Id,
                    ProcessState = process.State.ToString()
                });
            }

            // 3. Token lifecycle events (from tokens)
            foreach (var token in tokensList)
            {
                // Get element info: first try execution node (from database), then BPMN model
                string? elementType = null;
                string? elementName = null;

                // Try to get from execution node first (database has the actual executed data)
                if (executionNodeLookup.TryGetValue(token.CurrentElementId, out var execNode))
                {
                    elementType = execNode.NodeType;
                    elementName = execNode.NodeName;
                }

                // Fallback to BPMN model if execution node doesn't have the info
                if (string.IsNullOrEmpty(elementType) || string.IsNullOrEmpty(elementName))
                {
                    var (bpmnType, bpmnName) = GetElementInfo(bpmnContext, process.ProcessBpmnId, token.CurrentElementId);
                    elementType = elementType ?? bpmnType;
                    elementName = elementName ?? bpmnName;
                }

                // Token created
                historyEvents.Add(new HistoryEventDto
                {
                    EventType = "TokenCreated",
                    Timestamp = token.CreatedAt,
                    Description = $"Token {token.Id} created at element '{token.CurrentElementId}'",
                    ProcessId = process.Id,
                    TokenId = token.Id,
                    ElementId = token.CurrentElementId,
                    ElementType = elementType,
                    ElementName = elementName,
                    TokenState = token.State.ToString(),
                    IsExecutable = token.IsExecutable,
                    ScopeId = token.ScopeId,
                    ArrivedViaFlowId = token.ArrivedViaFlowId
                });

                // Token activated
                if (token.ActivatedAt.HasValue)
                {
                    historyEvents.Add(new HistoryEventDto
                    {
                        EventType = "TokenActivated",
                        Timestamp = token.ActivatedAt.Value,
                        Description = $"Token {token.Id} activated at element '{token.CurrentElementId}'",
                        ProcessId = process.Id,
                        TokenId = token.Id,
                        ElementId = token.CurrentElementId,
                        ElementType = elementType,
                        ElementName = elementName,
                        TokenState = token.State.ToString(),
                        IsExecutable = token.IsExecutable,
                        ScopeId = token.ScopeId,
                        ArrivedViaFlowId = token.ArrivedViaFlowId
                    });
                }

                // Token completed
                if (token.CompletedAt.HasValue)
                {
                    historyEvents.Add(new HistoryEventDto
                    {
                        EventType = "TokenCompleted",
                        Timestamp = token.CompletedAt.Value,
                        Description = $"Token {token.Id} completed at element '{token.CurrentElementId}'",
                        ProcessId = process.Id,
                        TokenId = token.Id,
                        ElementId = token.CurrentElementId,
                        ElementType = elementType,
                        ElementName = elementName,
                        TokenState = token.State.ToString(),
                        IsExecutable = token.IsExecutable,
                        ScopeId = token.ScopeId,
                        ArrivedViaFlowId = token.ArrivedViaFlowId
                    });
                }
            }

            // 4. Execution nodes (elements executed)
            foreach (var node in executionNodes)
            {
                // Use execution node data first (from database - this is the actual executed data)
                var finalElementType = node.NodeType;
                var finalElementName = node.NodeName;

                // Fallback to BPMN model only if execution node doesn't have the info
                if (string.IsNullOrEmpty(finalElementType) || string.IsNullOrEmpty(finalElementName))
                {
                    var (bpmnType, bpmnName) = GetElementInfo(bpmnContext, process.ProcessBpmnId, node.NodeId);
                    finalElementType = finalElementType ?? bpmnType;
                    finalElementName = finalElementName ?? bpmnName;
                }

                historyEvents.Add(new HistoryEventDto
                {
                    EventType = "ElementExecuted",
                    Timestamp = node.ExecutedAt,
                    Description = $"Element '{finalElementName ?? node.NodeId}' ({finalElementType}) executed",
                    ProcessId = process.Id,
                    TokenId = node.TokenId,
                    ElementId = node.NodeId,
                    ElementType = finalElementType,
                    ElementName = finalElementName,
                    ScopeId = node.ScopeId,
                    ArrivedViaFlowId = node.ArrivedViaFlowId
                });
            }

            // 5. Process completion
            if (process.CompletedAt.HasValue)
            {
                historyEvents.Add(new HistoryEventDto
                {
                    EventType = "ProcessCompleted",
                    Timestamp = process.CompletedAt.Value,
                    Description = $"Process completed",
                    ProcessId = process.Id,
                    ProcessState = process.State.ToString()
                });
            }

            // Sort by timestamp
            historyEvents = historyEvents.OrderBy(e => e.Timestamp).ToList();

            var result = new ProcessHistoryDto
            {
                ProcessId = process.Id,
                ProcessName = process.Name,
                ProcessBpmnId = process.ProcessBpmnId,
                DeploymentId = process.DeploymentId,
                State = process.State,
                CreatedAt = process.CreatedAt,
                StartedAt = process.StartedAt,
                CompletedAt = process.CompletedAt,
                TotalTokens = tokensList.Count,
                TotalExecutionNodes = executionNodes.Count,
                HistoryEvents = historyEvents
            };

            _logger.LogInformation("Returning history for process {ProcessId} with {EventCount} events",
                processId, historyEvents.Count);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting history for process {ProcessId}", processId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get element type and name from BPMN model
    /// </summary>
    private (string? elementType, string? elementName) GetElementInfo(
        BpmnRuntimeContext? bpmnContext,
        string processBpmnId,
        string elementId)
    {
        if (bpmnContext == null || string.IsNullOrWhiteSpace(elementId))
            return (null, null);

        try
        {
            var element = bpmnContext.Model.GetElementById(processBpmnId, elementId);
            if (element == null)
                return (null, null);

            var elementType = GetElementType(element);
            var elementName = GetElementName(element);

            return (elementType, elementName);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get element info for {ElementId} in process {ProcessBpmnId}", elementId, processBpmnId);
            return (null, null);
        }
    }

    /// <summary>
    /// Get element type string from BPMN element
    /// </summary>
    private string GetElementType(BpmnFlowElement element)
    {
        if (element == null) return "Unknown";

        return element.GetType().Name switch
        {
            "BpmnStartEvent" => "StartEvent",
            "BpmnEndEvent" => "EndEvent",
            "BpmnUserTask" => "UserTask",
            "BpmnScriptTask" => "ScriptTask",
            "BpmnServiceTask" => "ServiceTask",
            "BpmnBoundaryEvent" => "BoundaryEvent",
            "BpmnIntermediateCatchEvent" => "IntermediateCatchEvent",
            "BpmnIntermediateThrowEvent" => "IntermediateThrowEvent",
            "BpmnExclusiveGateway" => "ExclusiveGateway",
            "BpmnParallelGateway" => "ParallelGateway",
            "BpmnInclusiveGateway" => "InclusiveGateway",
            "BpmnEventBasedGateway" => "EventBasedGateway",
            _ => element.GetType().Name
        };
    }

    /// <summary>
    /// Get element name from BPMN element
    /// </summary>
    private string? GetElementName(BpmnFlowElement element)
    {
        if (element == null) return null;

        // Try to get name property using reflection
        var nameProperty = element.GetType().GetProperty("name");
        return nameProperty?.GetValue(element) as string;
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

/// <summary>
/// Process with model information DTO
/// </summary>
public record ProcessWithModelDto
{
    public ProcessDetailDto Process { get; init; } = default!;
    public string? DeploymentKey { get; init; }
    public int? DeploymentVersion { get; init; }
    public string? BpmnModel { get; init; }
    public bool HasModel { get; init; }
}

/// <summary>
/// Process execution history DTO
/// </summary>
public record ProcessHistoryDto
{
    public Guid ProcessId { get; init; }
    public string ProcessName { get; init; } = default!;
    public string ProcessBpmnId { get; init; } = default!;
    public Guid DeploymentId { get; init; }
    public ProcessState State { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int TotalTokens { get; init; }
    public int TotalExecutionNodes { get; init; }
    public IReadOnlyCollection<HistoryEventDto> HistoryEvents { get; init; } = Array.Empty<HistoryEventDto>();
}

/// <summary>
/// History event DTO for timeline
/// </summary>
public record HistoryEventDto
{
    public string EventType { get; init; } = default!; // ProcessCreated, ProcessStarted, TokenCreated, TokenActivated, TokenCompleted, ElementExecuted, ProcessCompleted
    public DateTime Timestamp { get; init; }
    public string Description { get; init; } = default!;
    public Guid ProcessId { get; init; }
    public Guid? TokenId { get; init; }
    public string? ElementId { get; init; }
    public string? ElementType { get; init; }
    public string? ElementName { get; init; }
    public string? ProcessState { get; init; }
    public string? TokenState { get; init; }
    public bool? IsExecutable { get; init; }
    public Guid? ScopeId { get; init; }
    public string? ArrivedViaFlowId { get; init; }
}
