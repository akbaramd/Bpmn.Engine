using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

public interface ITokenExecutionDispatcher
{
    Task DispatchAsync(Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx, CancellationToken ct);
}

public interface IBpmnElementHandler
{
    bool CanHandle(BpmnFlowElement element);
    Task HandleAsync(Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx, CancellationToken ct);
}

public sealed class TokenExecutionDispatcher : ITokenExecutionDispatcher
{
    private readonly ILogger<TokenExecutionDispatcher> _logger;
    private readonly IEnumerable<IBpmnElementHandler> _handlers;

    public TokenExecutionDispatcher(IServiceProvider serviceProvider, ILogger<TokenExecutionDispatcher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _handlers = serviceProvider.GetServices<IBpmnElementHandler>() ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public Task DispatchAsync(Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx, CancellationToken ct)
    {
        var elementType = element.GetType().Name;
        var elementId = element.id ?? "unknown";

        _logger.LogDebug(
            "[DISPATCH] Starting dispatch. ProcessId={ProcessId} TokenId={TokenId} ElementType={ElementType} ElementId={ElementId}",
            process.Id,
            token.Id,
            elementType,
            elementId);

        var matches = _handlers.Where(h => h.CanHandle(element)).ToList();

        _logger.LogDebug(
            "[DISPATCH] Handler matching. ElementType={ElementType} ElementId={ElementId} Matches={MatchesCount} Handlers={Handlers}",
            elementType,
            elementId,
            matches.Count,
            string.Join(", ", matches.Select(m => m.GetType().Name)));

        if (matches.Count == 0)
        {
            var error = $"No handler found for element type '{elementType}' (ElementId={elementId})";
            _logger.LogError(
                "[DISPATCH] ❌ {Error} ProcessId={ProcessId} TokenId={TokenId}",
                error,
                process.Id,
                token.Id);

            token.Fail(error);
            return Task.CompletedTask;
        }

        if (matches.Count > 1)
        {
            var warning = $"Multiple handlers found for element type '{elementType}' (ElementId={elementId}). Using first handler.";
            _logger.LogWarning(
                "[DISPATCH] ⚠️ {Warning} ProcessId={ProcessId} TokenId={TokenId} Handlers={Handlers}",
                warning,
                process.Id,
                token.Id,
                string.Join(", ", matches.Select(m => m.GetType().Name)));
        }

        var handler = matches.First();

        _logger.LogInformation(
            "[DISPATCH] ✅ Handler selected. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId} Handler={Handler}",
            process.Id,
            token.Id,
            elementId,
            handler.GetType().Name);

        return handler.HandleAsync(process, token, element, ctx, ct);
    }
}