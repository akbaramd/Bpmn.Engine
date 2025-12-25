namespace Novin.Bpmn.Engine.Domain.ValueObjects;

/// <summary>
/// Derived status for a process instance that provides more detailed information
/// than the base ProcessState, especially regarding incidents and blocked execution.
/// 
/// This addresses the issue where ProcessState=Running might be misleading when
/// there are failed tokens with open incidents (incident-driven execution).
/// </summary>
public enum ProcessDerivedStatus
{
    /// <summary>
    /// Process is created but not started yet
    /// </summary>
    Created,

    /// <summary>
    /// Process is running normally without any incidents
    /// </summary>
    Running,

    /// <summary>
    /// Process is running but has open incidents (failed tokens waiting for resolution)
    /// This is the incident-driven execution state where the process is blocked
    /// but not failed (can be recovered via retry/move/terminate)
    /// </summary>
    RunningWithIncidents,

    /// <summary>
    /// Process is suspended (manual suspension)
    /// </summary>
    Suspended,

    /// <summary>
    /// Process completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Process was terminated (manual termination)
    /// </summary>
    Terminated,

    /// <summary>
    /// Process failed (fail-fast policy - cannot be recovered)
    /// This is different from RunningWithIncidents where recovery is possible
    /// </summary>
    Failed
}

