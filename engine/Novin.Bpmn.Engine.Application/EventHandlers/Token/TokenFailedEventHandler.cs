using MediatR;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Events;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

public sealed class TokenFailedEventHandler : INotificationHandler<TokenFailedEvent>
{
    private readonly IProcessCompletionEvaluator _evaluator;
    private readonly ILogger<TokenFailedEventHandler> _logger;

    public TokenFailedEventHandler(
        IProcessCompletionEvaluator evaluator,
        ILogger<TokenFailedEventHandler> logger)
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async System.Threading.Tasks.Task Handle(TokenFailedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogError(
            "[TOKEN-ENDED] ❌ Token failed event received. TokenId={TokenId} ProcessId={ProcessId} ElementId={ElementId} Error={Error} IsExecutable={Executable} ScopeId={ScopeId} OccurredAt={OccurredAt}",
            notification.TokenId,
            notification.ProcessId,
            notification.ElementId,
            notification.Error,
            notification.IsExecutable,
            notification.ScopeId,
            notification.OccurredAtUtc);

        _logger.LogDebug(
            "[TOKEN-ENDED] Evaluating process completion (policy: fail = evaluate completion). ProcessId={ProcessId}",
            notification.ProcessId);

        // Policy: Fail = Process Failed + terminate all other tokens
        // این منطق را می‌توان در یک handler جداگانه هم پیاده کرد
        await _evaluator.EvaluateCompletionAsync(notification.ProcessId, cancellationToken);

        _logger.LogTrace(
            "[TOKEN-ENDED] Completion evaluation finished. ProcessId={ProcessId} TokenId={TokenId}",
            notification.ProcessId,
            notification.TokenId);
    }
}