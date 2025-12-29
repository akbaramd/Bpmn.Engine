using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Queries.GetProcessExecutionFlow;

/// <summary>
/// DTO containing the complete execution flow for BPMN visualization
/// </summary>
public sealed class ProcessExecutionFlowDto
{
    public Guid ProcessId { get; init; }
    public string ProcessName { get; init; } = default!;
    public string ProcessBpmnId { get; init; } = default!;
    public Guid DeploymentId { get; init; }
    public ProcessState State { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// All BPMN elements that were executed (had tokens pass through)
    /// </summary>
    public IReadOnlyCollection<ExecutedElementDto> ExecutedElements { get; init; } = Array.Empty<ExecutedElementDto>();

    /// <summary>
    /// All sequence flows that were executed
    /// </summary>
    public IReadOnlyCollection<ExecutedFlowDto> ExecutedFlows { get; init; } = Array.Empty<ExecutedFlowDto>();

    /// <summary>
    /// Boundary events that were configured (active, triggered, or canceled)
    /// </summary>
    public IReadOnlyCollection<BoundaryEventDto> BoundaryEvents { get; init; } = Array.Empty<BoundaryEventDto>();

    /// <summary>
    /// Execution cycles/scopes that were created during execution
    /// </summary>
    public IReadOnlyCollection<ExecutionCycleDto> ExecutionCycles { get; init; } = Array.Empty<ExecutionCycleDto>();

    /// <summary>
    /// Summary statistics
    /// </summary>
    public ExecutionStatsDto Stats { get; init; } = new();
}