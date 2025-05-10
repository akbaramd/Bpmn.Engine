using Novin.Bpmn.EventSourcing.Contracts;
using System;

namespace Novin.Bpmn.EventSourcing.Events;

/// <summary>
/// پیاده‌سازی پایه برای تمام رویدادهای BPMN
/// </summary>
public abstract record BpmnEvent : IBpmnEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();
    
    /// <inheritdoc />
    public required string ProcessInstanceId { get; init; }
    
    /// <inheritdoc />
    public string EventType => GetType().Name;
    
    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <inheritdoc />
    public string? UserId { get; init; }
    
    /// <inheritdoc />
    public long Position { get; init; }
    
    /// <inheritdoc />
    public long Key { get; init; }
    
    /// <inheritdoc />
    public string Intent { get; init; } = "CREATED";
    
    /// <inheritdoc />
    public int ProcessVersion { get; init; } = 1;
} 