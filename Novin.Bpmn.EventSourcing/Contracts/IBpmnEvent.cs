using System;

namespace Novin.Bpmn.EventSourcing.Contracts;

/// <summary>
/// رابط اصلی رویدادهای BPMN
/// </summary>
public interface IBpmnEvent
{
    /// <summary>
    /// شناسه رویداد
    /// </summary>
    Guid EventId { get; }
    
    /// <summary>
    /// شناسه نمونه فرآیند
    /// </summary>
    string InstanceId { get; }
    
    public string DeploymentKey { get; }
    public Guid DeploymentId { get; }
    public string? CorrelationId { get; }
    /// <summary>
    /// نوع رویداد
    /// </summary>
    string EventType { get; }

    DateTime Timestamp { get; }
    
} 