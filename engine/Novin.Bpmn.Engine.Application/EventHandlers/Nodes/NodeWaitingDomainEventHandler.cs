using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using NodeState = Novin.Bpmn.Engine.Domain.Entities.NodeState;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// When a NodeInstance goes to Waiting, make sure the Token is also put into Waiting (idempotent).
///
/// WHY:
/// - Some handlers only call node.Wait(...)
/// - Engine consistency requires token.Wait(...) too, because Token is the execution cursor and
///   is usually what resume/worker-correlation relies on.
///
/// Contract:
/// - Node.Wait(...) emits NodeWaitingDomainEvent (or similar)
/// - Token has Wait(workerId, reason)
/// - This handler runs inside the same UoW commit as domain events dispatch (outbox-first).
/// </summary>
public sealed class NodeWaitingDomainEventHandler : INotificationHandler<NodeWaitingDomainEvent>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<NodeWaitingDomainEventHandler> _logger;

    public NodeWaitingDomainEventHandler(
        IUnitOfWork uow,
        ILogger<NodeWaitingDomainEventHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(NodeWaitingDomainEvent notification, CancellationToken ct)
    {
        // 1) Load node (tracked)
        var node = await _uow.NodeInstances.GetByIdAsync(notification.NodeId, ct);
        if (node is null) return; // idempotent: deleted or already handled

        // must be waiting (if not, ignore)
        if (node.State != NodeState.Waiting)
            return;

        // 2) Load token (tracked)
        var token = await _uow.Tokens.GetByIdAsync(node.TokenId, ct);
        if (token is null) return;

        // 3) Idempotency: if token already waiting for same worker => no-op
        if (token.State == TokenState.Waiting)
        {
            return;
        }

        // 4) Put token in waiting
        var workerId = node.WorkerId ?? notification.WorkerId;
    

        var reason = notification.Reason  ?? "Waiting";
        token.Wait(reason);

        // 5) Persist (no SaveChanges here; outer UoW/outbox commits)
        await _uow.Tokens.UpdateAsync(token, ct);

        _logger.LogDebug(
            "[NODE-WAITING] Token set to Waiting. ProcessId={ProcessId} TokenId={TokenId} NodeId={NodeId} WorkerId={WorkerId}",
            token.ProcessId, token.Id, node.Id, workerId);
    }
}
