using Novin.Bpmn.EventSourcing.Contracts;
using System;

namespace Novin.Bpmn.EventSourcing.Events;

/// <summary>
/// رویداد فعال‌سازی گیت‌وی
/// </summary>
public record GatewayActivatedEvent : BpmnEvent
{
    /// <summary>
    /// شناسه گیت‌وی
    /// </summary>
    public required string GatewayId { get; init; }
    
    /// <summary>
    /// نوع گیت‌وی
    /// </summary>
    public required string GatewayType { get; init; }
} 