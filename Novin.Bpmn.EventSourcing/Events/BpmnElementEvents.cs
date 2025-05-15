using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Novin.Bpmn.EventSourcing.Events;

/// <summary>
/// رویداد المان BPMN
/// </summary>
public abstract record ElementEvent : BpmnEvent
{
      public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

     

        public required string          ElementId     { get; init; }
        public required string ElementType   { get; init; }  // نوع المان :contentReference[oaicite:4]{index=4}:contentReference[oaicite:5]{index=5}
        public Guid                 ExecutionId   { get; set; }
        public bool                   IsExecutable  { get; init; } = true;
        public int                    Version       { get; init; } = 1;
}
/// <summary>
/// رویداد ایجاد المان BPMN
/// </summary>
public record ElementCreated : ElementEvent
{
    /// <summary>
    /// Type of event - ElementCreated
    /// </summary>
    public override string EventType => "ElementCreated";
    
    /// <summary>
    /// شناسه المان منبع
    /// </summary>
    public string? SourceElementId { get; init; }
    
    /// <summary>
    /// شناسه جریان توالی
    /// </summary>
    public string? SequenceFlowId { get; init; }
}

/// <summary>
/// رویداد درحال پردازش المان BPMN
/// </summary>
public record ElementProcessing : ElementEvent
{
    /// <summary>
    /// Type of event - ElementProcessing
    /// </summary>
    public override string EventType => "ElementProcessing";
    
}

/// <summary>
/// رویداد تکمیل المان BPMN
/// </summary>
public record ElementCompleted : ElementEvent
{
    /// <summary>
    /// Type of event - ElementCompleted
    /// </summary>
    public override string EventType => "ElementCompleted";
}

/// <summary>
/// رویداد شکست المان BPMN
/// </summary>
public record ElementFailed : ElementEvent
{
    /// <summary>
    /// Type of event - ElementFailed
    /// </summary>
    public override string EventType => "ElementFailed";
    
    /// <summary>
    /// کد خطا
    /// </summary>
    public string? ErrorCode { get; init; }
    
    /// <summary>
    /// پیام خطا
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// آیا رویداد مرزی خطا دارد
    /// </summary>
    public bool HasErrorBoundaryEvent { get; init; }
    
    /// <summary>
    /// شناسه رویداد مرزی خطا
    /// </summary>
    public string? ErrorBoundaryEventId { get; init; }
}

/// <summary>
/// رویداد خاتمه المان BPMN
/// </summary>
public record ElementTerminated : ElementEvent
{
    /// <summary>
    /// Type of event - ElementTerminated
    /// </summary>
    public override string EventType => "ElementTerminated";
} 