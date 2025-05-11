using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Collections.Generic;

namespace Novin.Bpmn.EventSourcing.Events;

/// <summary>
/// رویداد آغاز زیرفرآیند
/// </summary>
public record SubProcessStartingEvent : BpmnEvent
{
    /// <summary>
    /// شناسه زیرفرآیند
    /// </summary>
    public required string SubProcessId { get; init; }
    
    /// <summary>
    /// اطلاعات اضافی درباره زیرفرآیند
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    public string? ExecutionId { get; set; }
} 