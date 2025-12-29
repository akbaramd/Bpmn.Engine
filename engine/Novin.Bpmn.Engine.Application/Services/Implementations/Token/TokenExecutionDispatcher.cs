using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Exceptions;
using Novin.Bpmn.Models.Models;


public sealed class NodeExecutionDispatcher : INodeExecutionDispatcher
{
    private readonly ILogger<NodeExecutionDispatcher> _logger;
    private readonly IBpmnElementHandler[] _handlers;

    public NodeExecutionDispatcher(
        IEnumerable<IBpmnElementHandler> handlers,
        ILogger<NodeExecutionDispatcher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _handlers = handlers?.ToArray() ?? throw new ArgumentNullException(nameof(handlers));
    }

    public async Task<ElementProcessResult> DispatchProcessAsync(
        Process process,
        Token token,
        NodeInstance node,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (token is null) throw new ArgumentNullException(nameof(token));
        if (node is null) throw new ArgumentNullException(nameof(node));
        if (element is null) throw new ArgumentNullException(nameof(element));
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        var (handler, elementId, elementType) = SelectHandlerOrThrow(process, token, node, element, phase: "PROC", isResume);

        _logger.LogInformation(
            "[NODE:DISPATCH:PROC] ✅ Selected. ProcessId={ProcessId} TokenId={TokenId} NodeId={NodeId} ElementType={ElementType} ElementId={ElementId} Handler={Handler} IsResume={IsResume} NodeState={NodeState}",
            process.Id, token.Id, node.Id, elementType, elementId, handler.GetType().Name, isResume, node.State);

        return await handler.ProcessAsync(process, token,node, element, ctx, isResume, ct);
    }

    public async Task DispatchNavigateAsync(
        Process process,
        Token token,
        NodeInstance node,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (token is null) throw new ArgumentNullException(nameof(token));
        if (node is null) throw new ArgumentNullException(nameof(node));
        if (element is null) throw new ArgumentNullException(nameof(element));
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        var (handler, elementId, elementType) = SelectHandlerOrThrow(process, token, node, element, phase: "NAV", isResume);

        _logger.LogInformation(
            "[NODE:DISPATCH:NAV] ✅ Selected. ProcessId={ProcessId} TokenId={TokenId} NodeId={NodeId} ElementType={ElementType} ElementId={ElementId} Handler={Handler} IsResume={IsResume} NodeState={NodeState}",
            process.Id, token.Id, node.Id, elementType, elementId, handler.GetType().Name, isResume, node.State);

        await handler.NavigateAsync(process, token,node, element, ctx, isResume, ct);
    }

    private (IBpmnElementHandler handler, string elementId, string elementType) SelectHandlerOrThrow(
        Process process,
        Token token,
        NodeInstance node,
        BpmnFlowElement element,
        string phase,
        bool isResume)
    {
        var elementType = element.GetType().Name;
        var elementId = element.id ?? node.ElementId ?? "unknown";

        _logger.LogDebug(
            "[NODE:DISPATCH:{Phase}] Start. ProcessId={ProcessId} TokenId={TokenId} NodeId={NodeId} ElementType={ElementType} ElementId={ElementId} IsResume={IsResume} NodeState={NodeState}",
            phase, process.Id, token.Id, node.Id, elementType, elementId, isResume, node.State);

        IBpmnElementHandler? first = null;
        IBpmnElementHandler? second = null;

        // low-allocation loop
        for (var i = 0; i < _handlers.Length; i++)
        {
            var h = _handlers[i];
            if (!h.CanHandle(element)) continue;

            if (first is null) first = h;
            else
            {
                second = h;
                break;
            }
        }

        if (first is null)
        {
            var error = $"No handler found for element type '{elementType}' (ElementId={elementId})";
            _logger.LogError(
                "[NODE:DISPATCH:{Phase}] ❌ {Error} ProcessId={ProcessId} TokenId={TokenId} NodeId={NodeId}",
                phase, error, process.Id, token.Id, node.Id);

            // if you prefer a NodeExecutionException, create one; reuse TokenExecutionException for now.
            throw new TokenExecutionException(process.Id, token.Id, elementId, error);
        }

        if (second is not null)
        {
            _logger.LogWarning(
                "[NODE:DISPATCH:{Phase}] ⚠️ Multiple handlers found. Using first. ProcessId={ProcessId} TokenId={TokenId} NodeId={NodeId} ElementType={ElementType} ElementId={ElementId} First={First} Second={Second}",
                phase, process.Id, token.Id, node.Id, elementType, elementId, first.GetType().Name, second.GetType().Name);
        }

        return (first, elementId, elementType);
    }
}
