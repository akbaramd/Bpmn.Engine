using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Service for recording minimal execution data for BPMN processes
/// Tracks only IsExecutable=true nodes from start to end events
/// </summary>
public interface IProcessExecutionRecorder
{
    /// <summary>
    /// Record when a node is executed (only for IsExecutable=true tokens)
    /// </summary>
    Task RecordNodeExecutionAsync(
        Process process,
        Token token,
        string nodeId,
        string? arrivedViaFlowId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Mark a node as completed when its execution finishes
    /// </summary>
    Task MarkNodeCompletedAsync(
        Guid processId,
        string nodeId,
        CancellationToken ct = default);

    /// <summary>
    /// Get the execution path for a process
    /// </summary>
    Task<IEnumerable<ExecutedNode>> GetExecutionPathAsync(
        Guid processId,
        CancellationToken ct = default);

    /// <summary>
    /// Get execution statistics for a process
    /// </summary>
    Task<ProcessExecutionStats> GetExecutionStatsAsync(
        Guid processId,
        CancellationToken ct = default);
}