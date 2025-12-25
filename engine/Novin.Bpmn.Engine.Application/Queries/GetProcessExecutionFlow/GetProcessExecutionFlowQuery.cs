using MediatR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Queries.GetProcessExecutionFlow;

/// <summary>
/// Query to get the complete execution flow of a process instance
/// for BPMN visualization in the client
/// </summary>
public sealed record GetProcessExecutionFlowQuery(
    Guid ProcessId
) : IRequest<ProcessExecutionFlowDto>;

/// <summary>
/// DTO containing the complete execution flow for BPMN visualization
/// </summary>
public sealed class ProcessExecutionFlowDto
{
    public Guid ProcessId { get; init; }
    public string ProcessName { get; init; } = default!;
    public string ProcessDefinitionId { get; init; } = default!;
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

/// <summary>
/// BPMN element that was executed
/// </summary>
public sealed class ExecutedElementDto
{
    public string ElementId { get; init; } = default!;
    public string ElementType { get; init; } = default!; // StartEvent, UserTask, EndEvent, etc.
    public string? ElementName { get; init; }
    public DateTime FirstExecutedAt { get; init; }
    public int ExecutionCount { get; init; } // How many times this element was executed
    public IReadOnlyCollection<TokenExecutionDto> TokenExecutions { get; init; } = Array.Empty<TokenExecutionDto>();
}

/// <summary>
/// Sequence flow that was executed
/// </summary>
public sealed class ExecutedFlowDto
{
    public string FlowId { get; init; } = default!;
    public string SourceElementId { get; init; } = default!;
    public string TargetElementId { get; init; } = default!;
    public string? FlowName { get; init; }
    public string? ConditionExpression { get; init; } // For conditional flows
    public DateTime FirstExecutedAt { get; init; }
    public int ExecutionCount { get; init; }
    public IReadOnlyCollection<TokenExecutionDto> TokenExecutions { get; init; } = Array.Empty<TokenExecutionDto>();
}

/// <summary>
/// Boundary event information
/// </summary>
public sealed class BoundaryEventDto
{
    public Guid SubscriptionId { get; init; }
    public string AttachedToElementId { get; init; } = default!;
    public string BoundaryEventId { get; init; } = default!;
    public BoundaryKind Kind { get; init; }
    public bool IsInterrupting { get; init; }
    public SubscriptionState State { get; init; }
    public string? ErrorCode { get; init; }
    public Guid? TokenScopeId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? TriggeredAt { get; init; }
}

/// <summary>
/// Execution cycle/scope information
/// </summary>
public sealed class ExecutionCycleDto
{
    public Guid ScopeId { get; init; }
    public string? ScopeName { get; init; } // Activity name that created the scope
    public Guid? ParentScopeId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int TokensInScope { get; init; }
    public int BoundaryEventsInScope { get; init; }
}

/// <summary>
/// Individual token execution through an element/flow
/// </summary>
public sealed class TokenExecutionDto
{
    public Guid TokenId { get; init; }
    public DateTime ExecutedAt { get; init; }
    public Guid? ScopeId { get; init; }
    public bool IsExecutable { get; init; }
}

/// <summary>
/// Execution statistics
/// </summary>
public sealed class ExecutionStatsDto
{
    public int TotalTokens { get; init; }
    public int ExecutedElements { get; init; }
    public int ExecutedFlows { get; init; }
    public int BoundaryEventsConfigured { get; init; }
    public int BoundaryEventsTriggered { get; init; }
    public int ExecutionCycles { get; init; }
    public TimeSpan? TotalExecutionTime { get; init; }
}