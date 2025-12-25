using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Events;

/// <summary>
/// Raised when a BPMN error occurs during process execution.
/// This event is handled by BoundarySubscriptionManager to find and trigger error boundary handlers.
/// </summary>
public sealed record ErrorRaisedEvent(
    Guid ProcessId,
    Guid TokenId,
    string ElementId,
    string ErrorCode,
    string ErrorMessage,
    Guid? ScopeId,
    DateTime OccurredAtUtc
) : IDomainEvent;
