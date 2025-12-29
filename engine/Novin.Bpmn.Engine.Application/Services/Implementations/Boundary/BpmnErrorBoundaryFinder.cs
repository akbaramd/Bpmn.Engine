using Microsoft.Extensions.Logging;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Implementation of IBpmnErrorBoundaryFinder that searches for error boundaries
/// and error event subprocesses in the BPMN model.
/// </summary>
public sealed class BpmnErrorBoundaryFinder : IBpmnErrorBoundaryFinder
{
    private readonly ILogger<BpmnErrorBoundaryFinder> _logger;

    public BpmnErrorBoundaryFinder(ILogger<BpmnErrorBoundaryFinder> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string? FindErrorBoundary(BpmnRuntimeContext ctx, string elementId, string errorCode)
    {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));
        if (string.IsNullOrWhiteSpace(elementId))
            throw new ArgumentException("Element ID cannot be null or empty", nameof(elementId));
        if (string.IsNullOrWhiteSpace(errorCode))
            throw new ArgumentException("Error code cannot be null or empty", nameof(errorCode));

        try
        {
            _logger.LogDebug(
                "[ERROR_BOUNDARY] Searching for error boundary. ElementId={ElementId} ErrorCode={ErrorCode} ProcessId={ProcessId}",
                elementId,
                errorCode,
                ctx.BpmnProcessId);

            // Get boundary events attached to the element
            var boundaryEvents = ctx.Model.GetBoundaryEvents(ctx.BpmnProcessId, elementId);
            
            _logger.LogDebug(
                "[ERROR_BOUNDARY] GetBoundaryEvents returned {Count} boundary events. ElementId={ElementId} ProcessId={ProcessId}",
                boundaryEvents.Count,
                elementId,
                ctx.BpmnProcessId);

            // Log all boundary events found (for debugging)
            foreach (var be in boundaryEvents)
            {
                _logger.LogDebug(
                    "[ERROR_BOUNDARY] Boundary event found. BoundaryEventId={BoundaryEventId} AttachedTo={AttachedTo} ElementId={ElementId}",
                    be.id,
                    be.attachedToRef?.Name,
                    elementId);
            }

            foreach (var boundaryEvent in boundaryEvents)
            {
                _logger.LogDebug(
                    "[ERROR_BOUNDARY] Checking boundary event. BoundaryEventId={BoundaryEventId} ElementId={ElementId}",
                    boundaryEvent.id,
                    elementId);

                // Check if this is an error boundary event
                if (IsErrorBoundaryEvent(ctx, boundaryEvent, errorCode))
                {
                    _logger.LogInformation(
                        "[ERROR_BOUNDARY] ✅ Found error boundary. ElementId={ElementId} BoundaryEventId={BoundaryEventId} ErrorCode={ErrorCode}",
                        elementId,
                        boundaryEvent.id,
                        errorCode);
                    
                    return boundaryEvent.id;
                }
                else
                {
                    _logger.LogDebug(
                        "[ERROR_BOUNDARY] Boundary event does not match error code. BoundaryEventId={BoundaryEventId} ElementId={ElementId} ErrorCode={ErrorCode}",
                        boundaryEvent.id,
                        elementId,
                        errorCode);
                }
            }

            _logger.LogDebug(
                "[ERROR_BOUNDARY] ❌ No error boundary found for element. ElementId={ElementId} ErrorCode={ErrorCode} BoundaryEventsCount={Count}",
                elementId,
                errorCode,
                boundaryEvents.Count);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ERROR_BOUNDARY] ❌ Exception while searching for error boundary. ElementId={ElementId} ErrorCode={ErrorCode}",
                elementId,
                errorCode);
            return null;
        }
    }

    public string? FindErrorEventSubprocess(BpmnRuntimeContext ctx, string errorCode)
    {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));
        if (string.IsNullOrWhiteSpace(errorCode))
            throw new ArgumentException("Error code cannot be null or empty", nameof(errorCode));

        try
        {
            // TODO: Implement error event subprocess search
            // This requires traversing the process hierarchy and finding subprocesses
            // with error start events that match the error code
            
            _logger.LogDebug(
                "[ERROR_BOUNDARY] Error event subprocess search not yet implemented. ErrorCode={ErrorCode}",
                errorCode);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ERROR_BOUNDARY] Error while searching for error event subprocess. ErrorCode={ErrorCode}",
                errorCode);
            return null;
        }
    }

    /// <summary>
    /// Checks if a boundary event is an error boundary event that can catch the given error code.
    /// </summary>
    private bool IsErrorBoundaryEvent(BpmnRuntimeContext ctx, BpmnBoundaryEvent boundaryEvent, string errorCode)
    {
        // Check if boundary event has error event definition
        if (boundaryEvent.Items == null)
        {
            _logger.LogDebug(
                "[ERROR_BOUNDARY] Boundary event has no items. BoundaryEventId={BoundaryEventId}",
                boundaryEvent.id);
            return false;
        }

        var errorEventDefinitions = boundaryEvent.Items.OfType<BpmnErrorEventDefinition>().ToList();
        
        if (errorEventDefinitions.Count == 0)
        {
            _logger.LogDebug(
                "[ERROR_BOUNDARY] Boundary event has no error event definitions. BoundaryEventId={BoundaryEventId}",
                boundaryEvent.id);
            return false;
        }

        _logger.LogDebug(
            "[ERROR_BOUNDARY] Found {Count} error event definitions in boundary event. BoundaryEventId={BoundaryEventId}",
            errorEventDefinitions.Count,
            boundaryEvent.id);

        foreach (var errorDef in errorEventDefinitions)
        {
            // If errorRef is null or empty, it catches all errors
            if (string.IsNullOrWhiteSpace(errorDef.errorRef?.Name))
            {
                _logger.LogDebug(
                    "[ERROR_BOUNDARY] Error boundary catches all errors (no errorRef). BoundaryEventId={BoundaryEventId}",
                    boundaryEvent.id);
                return true; // Catches all errors
            }

            // errorRef.Name is the ID of the error element, not the errorCode
            // We need to find the error element and check its errorCode attribute
            var errorElementId = errorDef.errorRef.Name;
            var errorElement = GetErrorElement(ctx, errorElementId);

            if (errorElement == null)
            {
                _logger.LogWarning(
                    "[ERROR_BOUNDARY] Error element not found. ErrorElementId={ErrorElementId} BoundaryEventId={BoundaryEventId}",
                    errorElementId,
                    boundaryEvent.id);
                continue;
            }

            // Compare errorCode from error element with the thrown error code
            var boundaryErrorCode = errorElement.errorCode;
            
            _logger.LogDebug(
                "[ERROR_BOUNDARY] Comparing error codes. BoundaryErrorCode={BoundaryErrorCode} ThrownErrorCode={ThrownErrorCode} ErrorElementId={ErrorElementId}",
                boundaryErrorCode,
                errorCode,
                errorElementId);

            if (string.IsNullOrWhiteSpace(boundaryErrorCode))
            {
                // If error element has no errorCode, it catches all errors
                _logger.LogDebug(
                    "[ERROR_BOUNDARY] Error element has no errorCode (catches all). ErrorElementId={ErrorElementId} BoundaryEventId={BoundaryEventId}",
                    errorElementId,
                    boundaryEvent.id);
                return true;
            }

            if (boundaryErrorCode == errorCode)
            {
                _logger.LogDebug(
                    "[ERROR_BOUNDARY] ✅ Error codes match. BoundaryErrorCode={BoundaryErrorCode} ThrownErrorCode={ThrownErrorCode} ErrorElementId={ErrorElementId}",
                    boundaryErrorCode,
                    errorCode,
                    errorElementId);
                return true; // Matches specific error code
            }
        }

        return false;
    }

    /// <summary>
    /// Gets an error element from BPMN definitions by its ID.
    /// </summary>
    private BpmnError? GetErrorElement(BpmnRuntimeContext ctx, string errorElementId)
    {
        try
        {
            var errorElement = ctx.Model.GetErrorElement(errorElementId);
            
            if (errorElement == null)
            {
                _logger.LogDebug(
                    "[ERROR_BOUNDARY] Error element not found in definitions. ErrorElementId={ErrorElementId}",
                    errorElementId);
            }
            else
            {
                _logger.LogDebug(
                    "[ERROR_BOUNDARY] Error element found. ErrorElementId={ErrorElementId} ErrorCode={ErrorCode} Name={Name}",
                    errorElementId,
                    errorElement.errorCode,
                    errorElement.name);
            }
            
            return errorElement;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ERROR_BOUNDARY] Exception while getting error element. ErrorElementId={ErrorElementId}",
                errorElementId);
            return null;
        }
    }
}

