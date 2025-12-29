using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Service for calculating derived process status based on process state,
/// tokens, and incidents. This provides more detailed status information
/// than the base ProcessState enum.
/// </summary>
public interface IProcessStatusService
{
    /// <summary>
    /// Calculates the derived status of a process instance.
    /// This considers:
    /// - Base ProcessState (Running, Suspended, Completed, etc.)
    /// - Presence of open incidents (indicates blocked execution)
    /// - Presence of failed tokens (indicates incident-driven execution)
    /// </summary>
    Task<ProcessDerivedStatus> GetDerivedStatusAsync(Process process, CancellationToken ct = default);
}

