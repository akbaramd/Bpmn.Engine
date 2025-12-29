using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.ProcessToken;
using Novin.Bpmn.Engine.Domain.Events;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Handles TokenMovedEvent and triggers processing at the new element.
/// This is the PRIMARY trigger for element processing after movement.
/// </summary>
public sealed class TokenMovedEventHandler : INotificationHandler<TokenMovedEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<TokenMovedEventHandler> _logger;

    public TokenMovedEventHandler(
        IMediator mediator,
        ILogger<TokenMovedEventHandler> _logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        this._logger = _logger ?? throw new ArgumentNullException(nameof(_logger));
    }

    public async System.Threading.Tasks.Task Handle(TokenMovedEvent notification, CancellationToken ct)
    {
        _logger.LogInformation(
            "[TOKEN-MOVED] TokenId={TokenId} ProcessId={ProcessId} From={FromElementId} To={ToElementId} ViaFlow={ViaFlowId} IsExecutable={IsExecutable}",
            notification.TokenId,
            notification.ProcessId,
            notification.FromElementId,
            notification.ToElementId,
            notification.ViaFlowId,
            notification.IsExecutable);

        // Process token at the new element
        var command = new ProcessTokenCommand(notification.ProcessId, notification.TokenId);
        await _mediator.Send(command, ct);
    }
}
