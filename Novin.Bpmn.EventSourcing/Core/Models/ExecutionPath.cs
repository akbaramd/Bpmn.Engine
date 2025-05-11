using System;
using System.Collections.Generic;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Events;

namespace Novin.Bpmn.EventSourcing.Core.Models;

/// <summary>
/// Tracks the execution path of a process instance
/// </summary>
public class ExecutionPath
{
    /// <summary>
    /// Unique ID for this execution path
    /// </summary>
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// The ID of the element where this execution started
    /// </summary>
    public string SourceElementId { get; set; } = null!;
    
    /// <summary>
    /// The type of the source element
    /// </summary>
    public string SourceElementType { get; set; } = null!;
    
    /// <summary>
    /// The ID of the target element where execution flowed to
    /// </summary>
    public string TargetElementId { get; set; } = null!;
    
    /// <summary>
    /// The type of the target element
    /// </summary>
    public string TargetElementType { get; set; } = null!;
    
    /// <summary>
    /// The sequence flow ID that connected source to target
    /// </summary>
    public string? SequenceFlowId { get; set; }
    
    /// <summary>
    /// Timestamp when this execution path was taken
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Parent execution ID if this is a child execution (e.g., from a parallel gateway)
    /// </summary>
    public string? ParentExecutionId { get; set; }
    
    /// <summary>
    /// Status of this execution path
    /// </summary>
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Active;
    
    /// <summary>
    /// Variables carried along this execution path (if different from process variables)
    /// </summary>
    public Dictionary<string, object>? LocalVariables { get; set; }
    
    /// <summary>
    /// Additional properties for this execution path
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = new();
    
    /// <summary>
    /// List of event IDs associated with this execution path
    /// </summary>
    public List<string> EventIds { get; set; } = new List<string>();
    
    /// <summary>
    /// Maps event types to count of events of that type in this execution
    /// </summary>
    public Dictionary<string, int> EventTypeCounts { get; set; } = new Dictionary<string, int>();
    
    /// <summary>
    /// Events in chronological order
    /// </summary>
    public List<IBpmnEvent> Events { get; set; } = new List<IBpmnEvent>();
    
    /// <summary>
    /// Get total count of events in this execution path
    /// </summary>
    public int TotalEventCount => EventIds.Count;
    
    /// <summary>
    /// Indicates whether this execution path is executable or just a placeholder (for failed conditions)
    /// </summary>
    public bool IsExecutable { get; set; } = true;
    
    /// <summary>
    /// Add an event to this execution path
    /// </summary>
    /// <param name="bpmnEvent">The BPMN event to add</param>
    public void AddEvent(IBpmnEvent bpmnEvent)
    {
        EventIds.Add(bpmnEvent.EventId.ToString());
        
        if (EventTypeCounts.ContainsKey(bpmnEvent.EventType))
        {
            EventTypeCounts[bpmnEvent.EventType]++;
        }
        else
        {
            EventTypeCounts[bpmnEvent.EventType] = 1;
        }
        
        Events.Add(bpmnEvent);
    }
}

/// <summary>
/// Status of an execution path
/// </summary>
public enum ExecutionStatus
{
    /// <summary>
    /// Execution is active
    /// </summary>
    Active,
    
    /// <summary>
    /// Execution has completed normally
    /// </summary>
    Completed,
    
    /// <summary>
    /// Execution has been terminated
    /// </summary>
    Terminated,
    
    /// <summary>
    /// Execution has failed
    /// </summary>
    Failed,
    
    /// <summary>
    /// Execution is waiting (e.g., for a timer or message)
    /// </summary>
    Waiting,
    
    /// <summary>
    /// Execution is suspended
    /// </summary>
    Suspended
}

/// <summary>
/// Represents an event in the execution path
/// </summary>
