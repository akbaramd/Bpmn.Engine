using System.Runtime.CompilerServices;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Domain.Events;

public sealed record TokenCreatedEvent(
    Guid TokenId,
    Guid ProcessId,
    string StartElementId,
    Guid? ParentTokenId,
    DateTime OccurredAtUtc
) : IDomainEvent;

public sealed record TokenActivatedEvent(
    Guid TokenId,
    Guid ProcessId,
    string ElementId,
    DateTime OccurredAtUtc,
    bool IsExecutable
) : IDomainEvent;


public sealed record TokenMovedEvent(
    Guid TokenId,
    Guid ProcessId,
    string FromElementId,
    string ToElementId,
    List<string> ViaFlowIds,
    DateTime OccurredAtUtc,
    bool IsExecutable,
    Guid? ScopeId,
    bool SkipProcess,
    Guid? ActivityInstanceId) : IDomainEvent;


public sealed record TokenWaitingEvent(
    Guid TokenId,
    Guid ProcessId,
    string ElementId,
    string? Reason,
    DateTime OccurredAtUtc,
    bool IsExecutable,
    Guid? ScopeId
) : IDomainEvent;
public record TokenArrivedViaFlowEvent(
    Guid TokenId, 
    Guid ProcessId, 
    string ElementId, 
    string ArrivedViaFlowId, 
    DateTime OccurredAtUtc, 
    bool IsExecutable, 
    Guid? ScopeId
) : IDomainEvent;
public sealed record TokenResumedEvent(
    Guid TokenId,
    Guid ProcessId,
    string ElementId,
    DateTime OccurredAtUtc,
    bool IsExecutable,
    Guid? ScopeId
) : IDomainEvent;

public sealed record TokenProcessedEvent(
    Guid TokenId,
    Guid ProcessId,
    string ElementId,
    DateTime OccurredAtUtc,
    bool IsExecutable,
    Guid? ScopeId
) : IDomainEvent;

public sealed record TokenFailedEvent(
    Guid TokenId,
    Guid ProcessId,
    string ElementId,
    string Error,
    DateTime OccurredAtUtc,
    bool IsExecutable,
    Guid? ScopeId,
    Guid? IncidentId = null,
    string? ErrorType = null,
    string? ErrorCode = null
) : IDomainEvent;

public sealed record TokenCompletedEvent(
    Guid TokenId,
    Guid ProcessId,
    string ElementId,
    string? Reason,
    DateTime OccurredAtUtc,
    bool IsExecutable,
    Guid? ScopeId
) : IDomainEvent;

public sealed record TokenTerminatedEvent(
    Guid TokenId,
    Guid ProcessId,
    string ElementId,
    string? Reason,
    DateTime OccurredAtUtc,
    bool IsExecutable,
    Guid? ScopeId
) : IDomainEvent;

public sealed record TokenRetriedEvent(
    Guid TokenId,
    Guid ProcessId,
    string ElementId,
    DateTime OccurredAtUtc,
    bool IsExecutable,
    Guid? ScopeId
) : IDomainEvent;

public sealed record TokenForkedEvent(
    Guid TokenId,
    Guid ProcessId,
    string ElementId,
    Guid ScopeId,
    int ChildCount,
    DateTime OccurredAtUtc,
    bool IsExecutable
) : IDomainEvent;

public sealed record TokenMergedEvent(
    Guid TokenId,
    Guid ProcessId,
    string ElementId,
    Guid ScopeId,
    Guid ParentTokenId,
    DateTime OccurredAtUtc,
    bool IsExecutable
) : IDomainEvent;

public sealed record TokenReactivatedFromForkedEvent(
    Guid TokenId,
    Guid ProcessId,
    string ElementId,
    Guid ScopeId,
    int MergedChildCount,
    DateTime OccurredAtUtc,
    bool IsExecutable
) : IDomainEvent;

// Optional observability events (nice to have)
public sealed record TokenBecameNonExecutableEvent(
    Guid TokenId,
    Guid ProcessId,
    string ElementId,
    DateTime OccurredAtUtc,
    Guid? ScopeId
) : IDomainEvent;

public sealed record TokenScopeAssignedEvent(
    Guid TokenId,
    Guid ProcessId,
    Guid ScopeId,
    DateTime OccurredAtUtc
) : IDomainEvent;

// Job Events
public sealed record WorkerCreatedEvent(
    Guid WorkerId,
    Guid ProcessId,
    Guid TokenId,
    string ElementId,
    string TaskName,
    DateTime OccurredAtUtc
) : IDomainEvent;

public sealed record WorkerStartedEvent(
    Guid WorkerId,
    Guid ProcessId,
    Guid TokenId,
    string ElementId,
    DateTime OccurredAtUtc,
    string? StartedBy = null
) : IDomainEvent;

public sealed record WorkerCompletedEvent(
    Guid WorkerId,
    Guid ProcessId,
    Guid TokenId,
    string ElementId,
    DateTime OccurredAtUtc,
    string? CompletedBy = null,
    Dictionary<string, string>? Result = null
) : IDomainEvent;

public sealed record WorkerFailedEvent(
    Guid WorkerId,
    Guid ProcessId,
    Guid TokenId,
    string ElementId,
    string ErrorMessage,
    DateTime OccurredAtUtc,
    string? FailedBy = null
) : IDomainEvent;

public sealed record WorkerTimedOutEvent(
    Guid WorkerId,
    Guid ProcessId,
    Guid TokenId,
    string ElementId,
    DateTime OccurredAtUtc
) : IDomainEvent;

// Boundary Subscription Events
public sealed record BoundarySubscriptionCreatedEvent(
    Guid SubscriptionId,
    Guid ProcessId,
    Guid TokenId,
    string ElementId,
    string BoundaryElementId,
    string? ErrorCode,
    string? MessageName,
    bool IsErrorHandler,
    bool IsMessageHandler,
    DateTime OccurredAtUtc
) : IDomainEvent;

public sealed record BoundarySubscriptionTriggeredEvent(
    Guid SubscriptionId,
    Guid ProcessId,
    Guid TokenId,
   Guid? ActivityInstanceId, 
    string ElementId,
    string BoundaryElementId,
    DateTime OccurredAtUtc,
    string? TriggerReason = null
) : IDomainEvent;

public sealed record BoundarySubscriptionCancelledEvent(
    Guid SubscriptionId,
    Guid ProcessId,
    Guid TokenId,
    string ElementId,
    string BoundaryElementId,
    DateTime OccurredAtUtc,
    string? CancelReason = null
) : IDomainEvent;

// Gateway Events
public sealed record GatewayDecisionMadeEvent(
    Guid ProcessId,
    Guid TokenId,
    string GatewayElementId,
    string DecisionType, // "Join", "Split", "Exclusive", "Inclusive", "Parallel", "Complex"
    IReadOnlyCollection<string> OutgoingFlows,
    DateTime OccurredAtUtc,
    Dictionary<string, string>? Context = null
) : IDomainEvent;

public sealed record GatewayWaitingForJoinEvent(
    Guid ProcessId,
    Guid TokenId,
    string GatewayElementId,
    int TokensArrived,
    int TokensRequired,
    DateTime OccurredAtUtc
) : IDomainEvent;

// Service Task Events
public sealed record ServiceTaskRoutedEvent(
    Guid ProcessId,
    Guid TokenId,
    Guid WorkerId,
    string ElementId,
    string TaskName,
    string? TargetClientId,
    string? Implementation,
    DateTime OccurredAtUtc
) : IDomainEvent;

public sealed record ServiceTaskAcknowledgedEvent(
    Guid ProcessId,
    Guid TokenId,
    Guid WorkerId,
    string ElementId,
    string AcknowledgedBy,
    DateTime OccurredAtUtc
) : IDomainEvent;

// Process Completion Events
public sealed record ProcessCompletionEvaluationEvent(
    Guid ProcessId,
    int TotalTokens,
    int LiveExecutableTokens,
    int LiveTraceTokens,
    int TerminalTokens,
    string EvaluationResult, // "Continue", "Completed", "Failed", "Suspended"
    DateTime OccurredAtUtc
) : IDomainEvent;

public sealed record ProcessAllTokensCompletedEvent(
    Guid ProcessId,
    DateTime OccurredAtUtc,
    string CompletionReason = "All executable tokens completed"
) : IDomainEvent;

public sealed record ProcessTraceTokensOnlyEvent(
    Guid ProcessId,
    int TraceTokenCount,
    DateTime OccurredAtUtc,
    string Reason = "Only trace tokens remain"
) : IDomainEvent;

// Token Processing Events
public sealed record TokenProcessingFailedEvent(
    Guid TokenId,
    Guid ProcessId,
    string ElementId,
    string ErrorMessage,
    string ErrorType, // "BpmnError", "TechnicalFailure"
    string? ErrorCode,
    DateTime OccurredAtUtc,
    bool IsExecutable,
    Guid? ScopeId
) : IDomainEvent;

public sealed record BpmnErrorOccurredEvent(
    Guid TokenId,
    Guid ProcessId,
    string ElementId,
    string ErrorCode,
    string ErrorMessage,
    Guid? ScopeId,
    DateTime OccurredAtUtc
) : IDomainEvent;

public sealed record TechnicalFailureOccurredEvent(
    Guid TokenId,
    Guid ProcessId,
    string ElementId,
    string ErrorMessage,
    string StackTrace,
    DateTime OccurredAtUtc
) : IDomainEvent;