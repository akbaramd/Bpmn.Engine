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
    public required string ProcessInstanceId { get; init; }
    
    public string ProcessDefinitionKey { get; init ; }

    /// <inheritdoc />
    public virtual string EventType => GetType().Name;
    public DateTime Timestamp { get; internal set; }

    
    
} 