using Microsoft.AspNetCore.Mvc;
using MediatR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Queries.GetProcessExecutionFlow;
using Novin.Bpmn.Engine.Application.Queries.GetIncidents;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using System.Linq;
using Novin.Bpmn.Engine.Application.Queries;
using Novin.Bpmn.Engine.Application.Services;
using NodeState = Novin.Bpmn.Engine.Domain.Entities.NodeState;

namespace Novin.Bpmn.Engine.Api.Controllers;

/// <summary>
/// Controller for process execution dashboards + BPMN modeler visualization.
/// Returns: executed nodes/flows, ALL tokens (no filtering), process variables, and basic process info.
/// </summary>
[ApiController]
[Route("api/process-execution")]
public sealed class ProcessExecutionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _uow;
    private readonly IBpmnRuntimeContextFactory _ctxFactory;

    public ProcessExecutionController(
        IMediator mediator,
        IUnitOfWork uow,
        IBpmnRuntimeContextFactory ctxFactory)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _ctxFactory = ctxFactory ?? throw new ArgumentNullException(nameof(ctxFactory));
    }

    // ----------------------------------------------------------------------
    // Existing endpoints (kept)
    // ----------------------------------------------------------------------

    /// <summary>Complete execution flow for client-side BPMN visualization.</summary>
    [HttpGet("{processId:guid}/flow")]
    [ProducesResponseType(typeof(ProcessExecutionFlowDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ProcessExecutionFlowDto>> GetExecutionFlow(Guid processId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetProcessExecutionFlowQuery(processId), ct);
        if (result == null) return NotFound(new { error = $"Process execution flow not found for {processId}" });
        return Ok(result);
    }

    /// <summary>Execution statistics (derived from execution flow query).</summary>
    [HttpGet("{processId:guid}/stats")]
    [ProducesResponseType(typeof(ExecutionStatsDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ExecutionStatsDto>> GetExecutionStats(Guid processId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetProcessExecutionFlowQuery(processId), ct);
        if (result == null) return NotFound(new { error = $"Process execution stats not found for {processId}" });
        return Ok(result.Stats);
    }

    /// <summary>Minimal execution path (audit trail) — executable nodes only (as your recorder defines).</summary>
    [HttpGet("{processId:guid}/path")]
    [ProducesResponseType(typeof(IEnumerable<NodeInstance>), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<IEnumerable<NodeInstance>>> GetExecutionPath(Guid processId, CancellationToken ct = default)
    {
        var nodeInstances = await _uow.NodeInstances.GetByProcessIdAsync(processId, ct);
        if (nodeInstances == null || !nodeInstances.Any()) 
            return NotFound(new { error = $"No execution path found for process {processId}" });
        return Ok(nodeInstances);
    }

    /// <summary>Execution stats from node instances.</summary>
    [HttpGet("{processId:guid}/audit-stats")]
    [ProducesResponseType(typeof(ProcessExecutionStats), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ProcessExecutionStats>> GetAuditStats(Guid processId, CancellationToken ct = default)
    {
        var nodeInstances = await _uow.NodeInstances.GetByProcessIdAsync(processId, ct);
        var stats = new ProcessExecutionStats
        {
            TotalNodes = nodeInstances.Count,
            CompletedNodes = nodeInstances.Count(x => x.State == NodeState.Completed),
            FailedNodes = nodeInstances.Count(x => x.State == NodeState.Failed),
            ActiveNodes = nodeInstances.Count(x => x.State == NodeState.Created)
        };

        return Ok(stats);
    }

    // ----------------------------------------------------------------------
    // NEW: Tokens endpoint (ALL tokens of the process, NO filtering)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Returns ALL tokens of the process (no filtering), including states and variables.
    /// This is ideal for dashboards and debugging.
    /// </summary>
    [HttpGet("{processId:guid}/tokens")]
    [ProducesResponseType(typeof(IReadOnlyList<TokenDashboardDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<IReadOnlyList<TokenDashboardDto>>> GetAllTokens(Guid processId, CancellationToken ct = default)
    {
        var process = await _uow.Processes.GetByIdAsync(processId, ct);
        if (process == null) return NotFound(new { error = $"Process not found: {processId}" });

        var tokens = (await _uow.Tokens.GetByProcessIdAsync(processId, ct)).ToList();

        var dto = tokens
            .OrderBy(t => t.CreatedAt)
            .Select(t => new TokenDashboardDto(
                TokenId: t.Id,
                ProcessId: t.ProcessId,
                CurrentElementId: t.CurrentElementId,
                State: t.State.ToString(),
                IsExecutable: t.IsExecutable,
                ScopeId: t.ScopeId,
                ArrivedViaFlowId: t.ArrivedViaFlowId,
                ActivityInstanceId: t.ActivityInstanceId,
                ParentTokenIds: t.ParentTokenIds?.ToArray() ?? Array.Empty<Guid>(),
                CreatedAtUtc: t.CreatedAt,
                ActivatedAtUtc: t.ActivatedAt,
                CompletedAtUtc: t.CompletedAt,
                Variables: t.Variables?.ToDictionary(k => k.Key, v => v.Value) ?? new Dictionary<string, string>()
            ))
            .ToList();

        return Ok(dto);
    }

    // ----------------------------------------------------------------------
    // NEW: Process info endpoint (variables + core process fields)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Returns process info for dashboards: state/timestamps/definition info + process variables.
    /// </summary>
    [HttpGet("{processId:guid}/process")]
    [ProducesResponseType(typeof(ProcessDashboardDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ProcessDashboardDto>> GetProcessInfo(Guid processId, CancellationToken ct = default)
    {
        var process = await _uow.Processes.GetByIdAsync(processId, ct);
        if (process == null) return NotFound(new { error = $"Process not found: {processId}" });

        var dto = new ProcessDashboardDto(
            ProcessId: process.Id,
            Name: process.Name,
            State: process.State.ToString(),
            DeploymentId: process.DeploymentId,
            ProcessDefinitionId: process.ProcessBpmnId,
            ProcessBpmnId: process.ProcessBpmnId,
            StartedAtUtc: process.StartedAtUtc,
            CompletedAtUtc: process.CompletedAtUtc,
            Variables: process.Variables?.ToDictionary(k => k.Key, v => v.Value) ?? new Dictionary<string, string>()
        );

        return Ok(dto);
    }

    // ----------------------------------------------------------------------
    // NEW: Full dashboard endpoint (elements + flows + tokens + variables)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Full dashboard payload for a process instance:
    /// - BPMN elements (nodes) and sequence flows (from model)
    /// - Execution flow (from your query/recorder)
    /// - ALL tokens (no filtering) with states + variables
    /// - Process info + process variables
    /// </summary>
    [HttpGet("{processId:guid}/dashboard")]
    [ProducesResponseType(typeof(ProcessDashboardBundleDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ProcessDashboardBundleDto>> GetDashboard(Guid processId, CancellationToken ct = default)
    {
        var process = await _uow.Processes.GetByIdAsync(processId, ct);
        if (process == null) return NotFound(new { error = $"Process not found: {processId}" });

        // Build BPMN runtime context (model)
        var ctx = await _ctxFactory.CreateAsync(process, ct);

        // Model nodes/flows for modeler highlighting
        var model = new BpmnModelDto(
            BpmnProcessId: ctx.BpmnProcessId,
            Elements: ctx.Model.GetFlowElements(ctx.BpmnProcessId)
                .Where(e => !string.IsNullOrWhiteSpace(e.id))
                .Select(e => new BpmnElementDto(
                    Id: e.id!,
                    Name: ReadName(e),
                    Type: e.GetType().Name
                ))
                .ToList(),
            Flows: ctx.Model.GetSequenceFlows(ctx.BpmnProcessId)
                .Where(f => !string.IsNullOrWhiteSpace(FlowKey(f)) && !string.IsNullOrWhiteSpace(f.sourceRef) && !string.IsNullOrWhiteSpace(f.targetRef))
                .Select(f => new BpmnFlowDto(
                    Id: FlowKey(f),
                    SourceRef: f.sourceRef!,
                    TargetRef: f.targetRef!,
                    Name: ReadName(f)
                ))
                .ToList()
        );

        // Execution flow (your existing query DTO)
        var executionFlow = await _mediator.Send(new GetProcessExecutionFlowQuery(processId), ct);

        // Tokens (ALL)
        var tokens = (await _uow.Tokens.GetByProcessIdAsync(processId, ct)).ToList();
        var tokenDtos = tokens
            .OrderBy(t => t.CreatedAt)
            .Select(t => new TokenDashboardDto(
                TokenId: t.Id,
                ProcessId: t.ProcessId,
                CurrentElementId: t.CurrentElementId,
                State: t.State.ToString(),
                IsExecutable: t.IsExecutable,
                ScopeId: t.ScopeId,
                ArrivedViaFlowId: t.ArrivedViaFlowId,
                ActivityInstanceId: t.ActivityInstanceId,
                ParentTokenIds: t.ParentTokenIds?.ToArray() ?? Array.Empty<Guid>(),
                CreatedAtUtc: t.CreatedAt,
                ActivatedAtUtc: t.ActivatedAt,
                CompletedAtUtc: t.CompletedAt,
                Variables: t.Variables?.ToDictionary(k => k.Key, v => v.Value) ?? new Dictionary<string, string>()
            ))
            .ToList();

        var processDto = new ProcessDashboardDto(
            ProcessId: process.Id,
            Name: process.Name,
            State: process.State.ToString(),
            DeploymentId: process.DeploymentId,
            ProcessDefinitionId: process.ProcessBpmnId,
            ProcessBpmnId: process.ProcessBpmnId,
            StartedAtUtc: process.StartedAtUtc,
            CompletedAtUtc: process.CompletedAtUtc,
            Variables: process.Variables?.ToDictionary(k => k.Key, v => v.Value) ?? new Dictionary<string, string>()
        );

        var bundle = new ProcessDashboardBundleDto(
            Process: processDto,
            Model: model,
            ExecutionFlow: executionFlow,
            Tokens: tokenDtos
        );

        return Ok(bundle);
    }

    // ----------------------------------------------------------------------
    // NEW: Incidents endpoint
    // ----------------------------------------------------------------------

    /// <summary>
    /// Returns all incidents for a specific process
    /// </summary>
    [HttpGet("{processId:guid}/incidents")]
    [ProducesResponseType(typeof(IReadOnlyList<IncidentDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<IReadOnlyList<IncidentDto>>> GetIncidents(Guid processId, CancellationToken ct = default)
    {
        var process = await _uow.Processes.GetByIdAsync(processId, ct);
        if (process == null) return NotFound(new { error = $"Process not found: {processId}" });

        var incidents = await _mediator.Send(new GetIncidentsQuery(processId), ct);
        return Ok(incidents.ToList());
    }

    // ----------------------------------------------------------------------
    // NEW: Node instance details endpoint
    // ----------------------------------------------------------------------

    /// <summary>
    /// Returns detailed information about a specific node instance
    /// </summary>
    [HttpGet("{processId:guid}/nodes/{nodeId:guid}")]
    [ProducesResponseType(typeof(NodeInstanceDetailDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<NodeInstanceDetailDto>> GetNodeDetails(Guid processId, Guid nodeId, CancellationToken ct = default)
    {
        var process = await _uow.Processes.GetByIdAsync(processId, ct);
        if (process == null) return NotFound(new { error = $"Process not found: {processId}" });

        var node = await _uow.NodeInstances.GetByIdAsync(nodeId, ct);
        if (node == null) return NotFound(new { error = $"Node instance not found: {nodeId}" });

        if (node.ProcessId != processId)
            return BadRequest(new { error = $"Node instance {nodeId} does not belong to process {processId}" });

        // Get associated token
        var token = node.TokenId != Guid.Empty 
            ? await _uow.Tokens.GetByIdAsync(node.TokenId, ct)
            : null;

        var dto = new NodeInstanceDetailDto(
            NodeId: node.Id,
            ProcessId: node.ProcessId,
            TokenId: node.TokenId,
            ElementId: node.ElementId,
            State: node.State.ToString(),
            IsExecutable: node.IsExecutable,
            ScopeId: node.ScopeId,
            ActivityInstanceId: node.ActivityInstanceId,
            ArrivedViaFlowIds: node.ArrivedViaFlowIds.ToList(),
            WorkerId: node.WorkerId,
            UserTaskId: node.UserTaskId,
            ErrorMessage: node.ErrorMessage,
            CreatedAtUtc: node.CreatedAtUtc,
            StartedAtUtc: node.StartedAtUtc,
            CompletedAtUtc: node.CompletedAtUtc,
            Variables: node.Variables.ToDictionary(k => k.Key, v => v.Value),
            Token: token != null ? new TokenDetailDto(
                TokenId: token.Id,
                ProcessId: token.ProcessId,
                CurrentElementId: token.CurrentElementId,
                State: token.State.ToString(),
                IsExecutable: token.IsExecutable,
                ScopeId: token.ScopeId,
                ArrivedViaFlowId: token.ArrivedViaFlowId,
                ActivityInstanceId: token.ActivityInstanceId,
                ParentTokenIds: token.ParentTokenIds.ToList(),
                CreatedAtUtc: token.CreatedAt,
                ActivatedAtUtc: token.ActivatedAt,
                CompletedAtUtc: token.CompletedAt,
                Variables: token.Variables.ToDictionary(k => k.Key, v => v.Value)
            ) : null
        );

        return Ok(dto);
    }

    // ----------------------------------------------------------------------
    // DTOs (keep here or move to Application.Contracts)
    // ----------------------------------------------------------------------

    public sealed record TokenDashboardDto(
        Guid TokenId,
        Guid ProcessId,
        string CurrentElementId,
        string State,
        bool IsExecutable,
        Guid? ScopeId,
        string? ArrivedViaFlowId,
        Guid? ActivityInstanceId,
        IReadOnlyList<Guid> ParentTokenIds,
        DateTime CreatedAtUtc,
        DateTime? ActivatedAtUtc,
        DateTime? CompletedAtUtc,
        IReadOnlyDictionary<string, string> Variables
    );

    public sealed record ProcessDashboardDto(
        Guid ProcessId,
        string? Name,
        string State,
        Guid DeploymentId,
        string? ProcessDefinitionId,
        string? ProcessBpmnId,
        DateTime? StartedAtUtc,
        DateTime? CompletedAtUtc,
        IReadOnlyDictionary<string, string> Variables
    );

    public sealed record BpmnModelDto(
        string BpmnProcessId,
        IReadOnlyList<BpmnElementDto> Elements,
        IReadOnlyList<BpmnFlowDto> Flows
    );

    public sealed record BpmnElementDto(
        string Id,
        string? Name,
        string Type
    );

    public sealed record BpmnFlowDto(
        string Id,
        string SourceRef,
        string TargetRef,
        string? Name
    );

    public sealed record ProcessDashboardBundleDto(
        ProcessDashboardDto Process,
        BpmnModelDto Model,
        ProcessExecutionFlowDto? ExecutionFlow,
        IReadOnlyList<TokenDashboardDto> Tokens
    );

    public sealed record NodeInstanceDetailDto(
        Guid NodeId,
        Guid ProcessId,
        Guid TokenId,
        string ElementId,
        string State,
        bool IsExecutable,
        Guid? ScopeId,
        Guid? ActivityInstanceId,
        IReadOnlyList<string> ArrivedViaFlowIds,
        Guid? WorkerId,
        Guid? UserTaskId,
        string? ErrorMessage,
        DateTime CreatedAtUtc,
        DateTime? StartedAtUtc,
        DateTime? CompletedAtUtc,
        IReadOnlyDictionary<string, string> Variables,
        TokenDetailDto? Token
    );

    public sealed record TokenDetailDto(
        Guid TokenId,
        Guid ProcessId,
        string CurrentElementId,
        string State,
        bool IsExecutable,
        Guid? ScopeId,
        string? ArrivedViaFlowId,
        Guid? ActivityInstanceId,
        IReadOnlyList<Guid> ParentTokenIds,
        DateTime CreatedAtUtc,
        DateTime? ActivatedAtUtc,
        DateTime? CompletedAtUtc,
        IReadOnlyDictionary<string, string> Variables
    );
    
    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static string? ReadName(object obj)
    {
        var t = obj.GetType();
        var p = t.GetProperty("name") ?? t.GetProperty("Name") ?? t.GetProperty("label") ?? t.GetProperty("Label");
        return p?.GetValue(obj) as string;
    }

    private static string FlowKey(Bpmn.Models.Models.BpmnSequenceFlow f)
        => !string.IsNullOrWhiteSpace(f.id) ? f.id! : $"{f.sourceRef}->{f.targetRef}";
}
