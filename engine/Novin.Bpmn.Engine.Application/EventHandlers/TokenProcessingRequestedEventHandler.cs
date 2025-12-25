using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Events;

public sealed class TokenProcessingRequestedEventHandler
    : INotificationHandler<TokenProcessingRequestedEvent>
{
    private readonly ITokenProcessingOrchestrator _orchestrator;
    private readonly IProcessExecutionRecorder _executionRecorder;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TokenProcessingRequestedEventHandler> _logger;

    public TokenProcessingRequestedEventHandler(
        ITokenProcessingOrchestrator orchestrator,
        IProcessExecutionRecorder executionRecorder,
        IUnitOfWork unitOfWork,
        ILogger<TokenProcessingRequestedEventHandler> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _executionRecorder = executionRecorder ?? throw new ArgumentNullException(nameof(executionRecorder));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(TokenProcessingRequestedEvent n, CancellationToken ct)
    {
        try
        {
            // Record execution for executable tokens only
            if (n.IsExecutable)
            {
                var process = await _unitOfWork.Processes.GetByIdAsync(n.ProcessId, ct);
                var token = await _unitOfWork.Tokens.GetByIdAsync(n.TokenId, ct);

                if (process != null && token != null)
                {
                    // Get node information (we'll use placeholder names for now - can be enhanced later)
                    var nodeName = GetNodeName(n.ElementId);
                    var nodeType = GetNodeType(n.ElementId);

                    await _executionRecorder.RecordNodeExecutionAsync(
                        process,
                        token,
                        n.ElementId,
                        nodeName,
                        nodeType,
                        n.ArrivedViaFlowId,
                        ct);

                    _logger.LogDebug(
                        "[EXECUTION-RECORD] Recorded node execution. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId} NodeType={NodeType}",
                        n.ProcessId,
                        n.TokenId,
                        n.ElementId,
                        nodeType);
                }
            }

            await _orchestrator.ProcessAsync(n.ProcessId, n.TokenId, ct);
        }
        catch (Exception ex)
        {
            // این catch فقط برای safety است - در حالت عادی نباید exception به اینجا برسد
            // چون Orchestrator همه exceptions را handle می‌کند
            _logger.LogError(
                ex,
                "[TOKEN-PROCESSING] ⚠️ Unexpected unhandled exception in token processing pipeline. ProcessId={ProcessId} TokenId={TokenId}",
                n.ProcessId,
                n.TokenId);

            // در اینجا می‌توانیم یک fallback incident ایجاد کنیم یا alert بفرستیم
            // اما exception را throw نمی‌کنیم تا event handler crash نکند
        }
    }

    /// <summary>
    /// Get human-readable node name (placeholder implementation)
    /// Can be enhanced to get actual names from BPMN context
    /// </summary>
    private static string GetNodeName(string elementId)
    {
        // For now, just return the element ID
        // This can be enhanced to get actual names from BPMN definitions
        return elementId;
    }

    /// <summary>
    /// Get node type from element ID (basic heuristic)
    /// Can be enhanced with BPMN context lookup
    /// </summary>
    private static string GetNodeType(string elementId)
    {
        // Basic heuristics based on common BPMN naming patterns
        if (elementId.StartsWith("StartEvent", StringComparison.OrdinalIgnoreCase) ||
            elementId.Contains("start", StringComparison.OrdinalIgnoreCase))
        {
            return "StartEvent";
        }

        if (elementId.StartsWith("EndEvent", StringComparison.OrdinalIgnoreCase) ||
            elementId.Contains("end", StringComparison.OrdinalIgnoreCase))
        {
            return "EndEvent";
        }

        if (elementId.Contains("Task", StringComparison.OrdinalIgnoreCase))
        {
            return "Task";
        }

        if (elementId.Contains("Gateway", StringComparison.OrdinalIgnoreCase))
        {
            return "Gateway";
        }

        if (elementId.Contains("Event", StringComparison.OrdinalIgnoreCase))
        {
            return "IntermediateEvent";
        }

        // Default fallback
        return "Task";
    }
}