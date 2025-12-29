using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.NodeDispatch;
using Novin.Bpmn.Engine.Application.Commands.NodeInstances;
using Novin.Bpmn.Engine.Domain.Events;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Reacts to TokenMovedEvent:
/// 1) Creates NodeInstance at new element
/// 2) Dispatches node processing
/// </summary>
public sealed class TokenMovedEventHandler : INotificationHandler<TokenMovedEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<TokenMovedEventHandler> _logger;

    public TokenMovedEventHandler(
        IMediator mediator,
        ILogger<TokenMovedEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(TokenMovedEvent e, CancellationToken ct)
    {
        _logger.LogInformation(
            "[TOKEN-MOVED] TokenId={TokenId} ProcessId={ProcessId} From={From} To={To} Executable={Executable}",
            e.TokenId, e.ProcessId, e.FromElementId, e.ToElementId, e.IsExecutable);

        // Trace / non-executable tokens DO NOT create nodes
        if (!e.IsExecutable)
        {
            _logger.LogDebug(
                "[TOKEN-MOVED] Skip node creation (non-executable token). TokenId={TokenId}",
                e.TokenId);
            return;
        }

        // 1) Create node for the new element
        var nodeId = await _mediator.Send(new CreateNodeInstanceCommand(
            ProcessId: e.ProcessId,
            TokenId: e.TokenId,
            ElementId: e.ToElementId,
            ScopeId: e.ScopeId,
            ActivityInstanceId: e.ActivityInstanceId,
            ArrivedViaFlowId: e.ViaFlowId
        ), ct);

        // 2) Dispatch node processing
        await _mediator.Send(new DispatchNodeProcessCommand(nodeId), ct);
    }
}