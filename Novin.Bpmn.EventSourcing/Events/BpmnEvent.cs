using Novin.Bpmn.EventSourcing.Contracts;
using System;

namespace Novin.Bpmn.EventSourcing.Events;

/// <summary>
/// پیاده‌سازی پایه برای تمام رویدادهای BPMN
/// </summary>
public record BpmnEvent : IBpmnEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();
    
    /// <inheritdoc />
    public required Guid InstanceId { get; init; }
    public required string ProcessId { get; init; }
    
    public required string DeploymentKey { get; init ; }
    public required Guid DeploymentId { get; init ; }
   public string?       CorrelationId         { get; init; }
    /// <inheritdoc />
    public virtual string EventType => GetType().Name;
    public DateTime Timestamp { get; internal set; }

    
    
} 