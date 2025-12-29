using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Exceptions;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class TokenExecutionDispatcher : ITokenExecutionDispatcher
{
    private readonly ILogger<TokenExecutionDispatcher> _logger;
    private readonly IBpmnElementHandler[] _handlers;

    public TokenExecutionDispatcher(
        IEnumerable<IBpmnElementHandler> handlers,
        ILogger<TokenExecutionDispatcher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _handlers = handlers?.ToArray() ?? throw new ArgumentNullException(nameof(handlers));
    }

    public async Task<ElementProcessResult> DispatchProcessAsync(
        Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (token is null) throw new ArgumentNullException(nameof(token));
        if (element is null) throw new ArgumentNullException(nameof(element));
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        var (handler, elementId, elementType) = SelectHandlerOrThrow(process, token, element, phase: "PROC", isResume);

        _logger.LogInformation(
            "[DISPATCH:PROC] ✅ Selected. ProcessId={ProcessId} TokenId={TokenId} ElementType={ElementType} ElementId={ElementId} Handler={Handler} IsResume={IsResume}",
            process.Id, token.Id, elementType, elementId, handler.GetType().Name, isResume);

        // Process stage
        return await handler.ProcessAsync(process, token, element, ctx, isResume, ct);
    }

    public async Task DispatchNavigateAsync(
        Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (token is null) throw new ArgumentNullException(nameof(token));
        if (element is null) throw new ArgumentNullException(nameof(element));
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        var (handler, elementId, elementType) = SelectHandlerOrThrow(process, token, element, phase: "NAV", isResume);

        _logger.LogInformation(
            "[DISPATCH:NAV] ✅ Selected. ProcessId={ProcessId} TokenId={TokenId} ElementType={ElementType} ElementId={ElementId} Handler={Handler} IsResume={IsResume}",
            process.Id, token.Id, elementType, elementId, handler.GetType().Name, isResume);

        // Navigation stage
        await handler.NavigateAsync(process, token, element, ctx, isResume, ct);
    }

    private (IBpmnElementHandler handler, string elementId, string elementType) SelectHandlerOrThrow(
        Process process,
        Token token,
        BpmnFlowElement element,
        string phase,
        bool isResume)
    {
        var elementType = element.GetType().Name;
        var elementId = element.id ?? "unknown";

        _logger.LogDebug(
            "[DISPATCH:{Phase}] Start. ProcessId={ProcessId} TokenId={TokenId} ElementType={ElementType} ElementId={ElementId} IsResume={IsResume}",
            phase, process.Id, token.Id, elementType, elementId, isResume);

        IBpmnElementHandler? first = null;
        IBpmnElementHandler? second = null;

        // کم‌allocation: یک loop
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
                "[DISPATCH:{Phase}] ❌ {Error} ProcessId={ProcessId} TokenId={TokenId}",
                phase, error, process.Id, token.Id);

            throw new TokenExecutionException(process.Id, token.Id, elementId, error);
        }

        if (second is not null)
        {
            _logger.LogWarning(
                "[DISPATCH:{Phase}] ⚠️ Multiple handlers found. Using first. ProcessId={ProcessId} TokenId={TokenId} ElementType={ElementType} ElementId={ElementId} First={First} Second={Second}",
                phase, process.Id, token.Id, elementType, elementId, first.GetType().Name, second.GetType().Name);
        }

        return (first, elementId, elementType);
    }
}