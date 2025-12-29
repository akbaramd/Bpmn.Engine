using Novin.Bpmn.Engine.Application.Services;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Service for finding Error Boundary or Error EventSubprocess that can catch a specific BPMN error code.
/// This implements BPMN 2.0 error propagation semantics.
/// </summary>
public interface IBpmnErrorBoundaryFinder
{
    /// <summary>
    /// Finds an error boundary event attached to the specified element that can catch the given error code.
    /// </summary>
    /// <param name="ctx">BPMN runtime context</param>
    /// <param name="elementId">Element ID where the error occurred</param>
    /// <param name="errorCode">BPMN error code to catch</param>
    /// <returns>Error boundary event ID if found, null otherwise</returns>
    string? FindErrorBoundary(BpmnRuntimeContext ctx, string elementId, string errorCode);

    /// <summary>
    /// Finds an error event subprocess in the process that can catch the given error code.
    /// </summary>
    /// <param name="ctx">BPMN runtime context</param>
    /// <param name="errorCode">BPMN error code to catch</param>
    /// <returns>Error event subprocess start event ID if found, null otherwise</returns>
    string? FindErrorEventSubprocess(BpmnRuntimeContext ctx, string errorCode);
}

