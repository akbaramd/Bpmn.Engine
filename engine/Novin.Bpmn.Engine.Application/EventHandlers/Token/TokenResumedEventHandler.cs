using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Events;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Handles TokenResumedEvent (after waiting) and triggers processing.
/// Resume is semantically "arrive again" at the same element.
/// </summary>
public sealed class TokenResumedEventHandler : INotificationHandler<TokenResumedEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<TokenResumedEventHandler> _logger;

    public TokenResumedEventHandler(
        IMediator mediator,
        ILogger<TokenResumedEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async System.Threading.Tasks.Task Handle(TokenResumedEvent notification, CancellationToken ct)
    {
        _logger.LogInformation(
            "[TOKEN-RESUMED] TokenId={TokenId} ProcessId={ProcessId} ElementId={ElementId} IsExecutable={IsExecutable}",
            notification.TokenId,
            notification.ProcessId,
            notification.ElementId,
            notification.IsExecutable);

        // Process token at current element (resume semantics)
        // Pass IsResume=true to indicate this is a resume operation
    }
}
