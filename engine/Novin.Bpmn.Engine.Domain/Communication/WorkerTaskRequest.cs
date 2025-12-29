using System;

namespace Novin.Bpmn.Engine.Domain.Communication;

/// <summary>
/// Request for worker task execution (user tasks or service tasks)
/// IDs are Guid, Variables and Metadata are Dictionary&lt;string, string&gt;
/// </summary>
public class WorkerTaskRequest
{
    /// <summary>
    /// Unique worker ID
    /// </summary>
    public Guid WorkerId { get; set; }

    /// <summary>
    /// Unique execution ID (legacy support)
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Process instance ID
    /// </summary>
    public Guid ProcessId { get; set; }

    /// <summary>
    /// Token ID
    /// </summary>
    public Guid TokenId { get; set; }

    /// <summary>
    /// BPMN element ID
    /// </summary>
    public string ElementId { get; set; } = string.Empty;

    /// <summary>
    /// Task name
    /// </summary>
    public string TaskName { get; set; } = string.Empty;

    /// <summary>
    /// Worker implementation (e.g., "SumNumbers" for service tasks, assignee for user tasks)
    /// </summary>
    public string Implementation { get; set; } = string.Empty;

    /// <summary>
    /// Worker variables (process variables and task-specific data) - all values as strings
    /// </summary>
    public Dictionary<string, string>? Variables { get; set; }

    /// <summary>
    /// Worker metadata (configuration and execution metadata) - all values as strings
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Additional payload data (legacy support) - all values as strings
    /// </summary>
    [Obsolete("Use Variables property instead")]
    public Dictionary<string, string>? Payload { get; set; }
}