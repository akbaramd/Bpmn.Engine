using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Application.Commands.ActivateToken;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Observes token creation to provide lifecycle telemetry and future hook points.
/// Execution is intentionally side-effect free to avoid interfering with the
/// existing processing pipeline (triggered by TokenProcessingRequested events).
/// </summary>
public sealed class TokenCreatedEventHandler : INotificationHandler<TokenCreatedEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<TokenCreatedEventHandler> _logger;

    public TokenCreatedEventHandler(
        IMediator mediator,
        ILogger<TokenCreatedEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async System.Threading.Tasks.Task Handle(TokenCreatedEvent notification, CancellationToken ct)
    {
        _logger.LogInformation(
            "[TOKEN-CREATED] TokenId={TokenId} ProcessId={ProcessId} StartElement={StartElementId} Parents={Parents} OccurredAt={OccurredAtUtc}",
            notification.TokenId,
            notification.ProcessId,
            notification.StartElementId,
            notification.ParentTokenIds.Count,
            notification.OccurredAtUtc);

        var command = new ActivateTokenCommand(
            ProcessId: notification.ProcessId,
            TokenId: notification.TokenId,
            ArrivedViaFlowId: null);

        await _mediator.Send(command, ct);
    }
}
