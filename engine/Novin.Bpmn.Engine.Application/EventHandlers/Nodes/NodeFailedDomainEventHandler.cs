using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

public sealed class NodeFailedDomainEventHandler
    : INotificationHandler<NodeFailedDomainEvent>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<NodeFailedDomainEventHandler> _logger;

    public NodeFailedDomainEventHandler(
        IUnitOfWork uow,
        ILogger<NodeFailedDomainEventHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(NodeFailedDomainEvent e, CancellationToken ct)
    {
        _logger.LogError(
            "[NODE-FAILED] NodeId={NodeId} TokenId={TokenId} ElementId={ElementId} Error={Error}",
            e.NodeId, e.TokenId, e.ElementId, e.ErrorMessage);

        // ✅ All DB changes in ONE transaction
        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            // 1) Load aggregates (tracked)
            var node = await _uow.NodeInstances.GetByIdAsync(e.NodeId, trxCt);
            if (node is null) return;

            var process = await _uow.Processes.GetByIdAsync(e.ProcessId, trxCt);
            if (process is null) return;

            var token = await _uow.Tokens.GetByIdAsync(e.TokenId, trxCt);
            if (token is null) return;

            
            
            token.Fail("node is faields");
            // 2) Find ACTIVE boundary subscriptions for this failed host element
            //    Prefer NodeInstanceId match (exact) then fallback to HostElementId.
            var subs = await _uow.BoundarySubscriptions.GetActiveErrorSubscriptionsByErrorCodeAsync(
                processId: process.Id,
                nodeInstanceId: node.Id,
                activityInstanceId: node.ActivityInstanceId ?? throw new InvalidOperationException(),
                ct: trxCt);

            if (subs.Count() == 0)
                return;

            // 3) Trigger error boundary subscriptions
            //    Note: NodeFailed = usually "technical" failure.
            //    If you only want BPMN Error boundaries, filter by Kind == Error.
            foreach (var sub in subs)
            {
                // only trigger Error boundary handlers here
                if (sub.Kind != BoundaryKind.Error)
                    continue;

                // If subscription has ErrorCode and you don't have one here, only trigger "catch-all" (null code)
                if (!string.IsNullOrWhiteSpace(sub.ErrorCode))
                    continue;

                sub.MarkTriggered(reason: $"Node failed: {e.ErrorMessage}");

                await _uow.BoundarySubscriptions.UpdateAsync(sub, trxCt);
            }

            // Optional: cancel other active subscriptions on same host if interrupting logic requires it
            // (usually handled when boundary is actually consumed by token movement)
        }, ct);
    }
}
