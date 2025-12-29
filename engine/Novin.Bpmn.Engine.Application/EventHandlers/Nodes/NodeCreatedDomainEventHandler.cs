using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.NodeDispatch;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.EventHandlers.NodeInstances;

/// <summary>
/// When a NodeInstance is created, we enqueue its processing.
/// IMPORTANT:
/// - Do not execute BPMN logic here.
/// - Keep idempotent.
/// - If NodeId == Guid.Empty (non-executable token path), ignore.
/// </summary>
public sealed class NodeCreatedDomainEventHandler : INotificationHandler<NodeCreatedDomainEvent>
{
    private readonly IMediator _mediator;
    private readonly INodeInstanceRepository _nodes;
    private readonly ILogger<NodeCreatedDomainEventHandler> _logger;

    public NodeCreatedDomainEventHandler(
        IMediator mediator,
        INodeInstanceRepository nodes,
        ILogger<NodeCreatedDomainEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(NodeCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        // Guard: CreateNodeInstanceCommandHandler may return Guid.Empty for non-executable tokens
        if (notification.NodeId == Guid.Empty)
            return;

        // Load node (idempotency + state guard)
        var node = await _nodes.GetByIdAsync(notification.NodeId, cancellationToken);
        if (node is null)
        {
            _logger.LogWarning("NodeCreatedDomainEvent received but NodeInstance not found. NodeId={NodeId}", notification.NodeId);
            return;
        }

        // If already started/completed/waiting, do nothing.
        if (node.State != NodeState.Created)
            return;

        // Enqueue processing
        await _mediator.Send(new DispatchNodeProcessCommand(node.Id), cancellationToken);

        _logger.LogDebug(
            "Node processing dispatched. NodeId={NodeId}, ProcessId={ProcessId}, TokenId={TokenId}, ElementId={ElementId}",
            node.Id, node.ProcessId, node.TokenId, node.ElementId);
    }
}
