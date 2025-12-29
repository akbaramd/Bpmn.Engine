namespace Novin.Bpmn.Engine.Clients.Abstractions;

/// <summary>
/// Attribute to define BPMN worker metadata
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class BpmnWorkerAttribute : Attribute
{
    /// <summary>
    /// The worker ID/implementation identifier
    /// </summary>
    public string WorkerId { get; }

    /// <summary>
    /// The work type this worker handles
    /// </summary>
    public string WorkType { get; }

    /// <summary>
    /// Display name for the worker (optional)
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Description of what this worker does (optional)
    /// </summary>
    public string? Description { get; set; }

    public BpmnWorkerAttribute(string workerId, string workType)
    {
        WorkerId = workerId ?? throw new ArgumentNullException(nameof(workerId));
        WorkType = workType ?? throw new ArgumentNullException(nameof(workType));
    }
}