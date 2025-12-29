using MediatR;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Events;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

public sealed class TokenTerminatedEventHandler : INotificationHandler<TokenTerminatedEvent>
{
    private readonly IProcessCompletionEvaluator _evaluator;
    private readonly ILogger<TokenTerminatedEventHandler> _logger;

    public TokenTerminatedEventHandler(
        IProcessCompletionEvaluator evaluator,
        ILogger<TokenTerminatedEventHandler> logger)
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async System.Threading.Tasks.Task Handle(TokenTerminatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[TOKEN-ENDED] ⚠️ Token terminated event received. TokenId={TokenId} ProcessId={ProcessId} ElementId={ElementId} Reason={Reason} IsExecutable={Executable} ScopeId={ScopeId} OccurredAt={OccurredAt}",
            notification.TokenId,
            notification.ProcessId,
            notification.ElementId,
            notification.Reason,
            notification.IsExecutable,
            notification.ScopeId,
            notification.OccurredAtUtc);

        _logger.LogDebug(
            "[TOKEN-ENDED] Evaluating process completion. ProcessId={ProcessId}",
            notification.ProcessId);

        await _evaluator.EvaluateCompletionAsync(notification.ProcessId, cancellationToken);

        _logger.LogTrace(
            "[TOKEN-ENDED] Completion evaluation finished. ProcessId={ProcessId} TokenId={TokenId}",
            notification.ProcessId,
            notification.TokenId);
    }
}