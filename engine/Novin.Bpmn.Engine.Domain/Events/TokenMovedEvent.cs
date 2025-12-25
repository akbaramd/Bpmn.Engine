using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

public sealed record TokenCreatedEvent(
    Guid TokenId,
    Guid ProcessId,
    string StartElementId,
    IReadOnlyCollection<Guid> ParentTokenIds,
    DateTime OccurredAtUtc
) : IDomainEvent;

public sealed record TokenActivatedEvent(
    Guid TokenId,
    Guid ProcessId,
    string ElementId,
    DateTime OccurredAtUtc,
    bool IsExecutable
) : IDomainEvent;

public sealed record TokenProcessingRequestedEvent(
    Guid TokenId,
    Guid ProcessId,
    string ElementId,
    DateTime OccurredAtUtc,
    bool IsExecutable,
    Guid? ScopeId,
    string? ArrivedViaFlowId
) : IDomainEvent;

public sealed record TokenMovedEvent(
    Guid TokenId,
    Guid ProcessId,
    string FromElementId,
    string ToElementId,
    string? ViaFlowId,
    DateTime OccurredAtUtc,
    bool IsExecutable,
    Guid? ScopeId
) : IDomainEvent;

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

public sealed record TokenCompletedEvent(
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