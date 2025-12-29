using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.ProcessToken;
using Novin.Bpmn.Engine.Domain.Events;
using System.Threading;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Handles TokenActivatedEvent and triggers processing at the current element.
/// This is needed for the first element (e.g., StartEvent) when token is created and activated.
/// After first move, TokenMovedEvent will trigger ProcessToken for subsequent elements.
/// </summary>
public sealed class TokenActivatedEventHandler : INotificationHandler<TokenActivatedEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<TokenActivatedEventHandler> _logger;

    public TokenActivatedEventHandler(
        IMediator mediator,
        ILogger<TokenActivatedEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async System.Threading.Tasks.Task Handle(TokenActivatedEvent notification, CancellationToken ct)
    {
        _logger.LogInformation(
            "[TOKEN-ACTIVATED] TokenId={TokenId} ProcessId={ProcessId} ElementId={ElementId} IsExecutable={IsExecutable} OccurredAt={OccurredAtUtc}",
            notification.TokenId,
            notification.ProcessId,
            notification.ElementId,
            notification.IsExecutable,
            notification.OccurredAtUtc);

        
        // Process token at current element (e.g., StartEvent)
        // This will dispatch to StartEventHandler which will call MoveToken
        var command = new ProcessTokenCommand(notification.ProcessId, notification.TokenId);
        await _mediator.Send(command, ct);
    }
}
