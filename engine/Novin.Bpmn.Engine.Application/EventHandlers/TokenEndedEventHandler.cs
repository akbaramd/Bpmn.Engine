using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Handles token terminal events (Completed/Terminated/Failed) and evaluates process completion.
/// This follows BPMN2 semantics: process completes when no live executable tokens remain.
/// </summary>
public sealed class TokenCompletedEventHandler : INotificationHandler<TokenCompletedEvent>
{
    private readonly IProcessCompletionEvaluator _evaluator;
    private readonly ILogger<TokenCompletedEventHandler> _logger;

    public TokenCompletedEventHandler(
        IProcessCompletionEvaluator evaluator,
        ILogger<TokenCompletedEventHandler> logger)
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(TokenCompletedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[TOKEN-ENDED] ✅ Token completed event received. TokenId={TokenId} ProcessId={ProcessId} ElementId={ElementId} IsExecutable={Executable} ScopeId={ScopeId} OccurredAt={OccurredAt}",
            notification.TokenId,
            notification.ProcessId,
            notification.ElementId,
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

    public async Task Handle(TokenTerminatedEvent notification, CancellationToken cancellationToken)
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

    public async Task Handle(TokenFailedEvent notification, CancellationToken cancellationToken)
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

