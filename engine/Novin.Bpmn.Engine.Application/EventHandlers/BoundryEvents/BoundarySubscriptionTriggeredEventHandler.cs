using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Commands.NodeInstances;
using Novin.Bpmn.Engine.Application.Commands.NodeDispatch;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using NodeState = Novin.Bpmn.Engine.Domain.Entities.NodeState;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Handles a triggered BoundaryEventSubscription:
/// - (optional) interrupt: cancels tokens/nodes in the same ActivityInstanceId and cancels other subscriptions
/// - spawns (or reuses) an execution to the boundary event element
/// - creates a NodeInstance for the boundary element and dispatches processing
/// </summary>
public sealed class BoundarySubscriptionTriggeredEventHandler
    : INotificationHandler<BoundarySubscriptionTriggeredEvent>
{
    private readonly IUnitOfWork _uow;
    private readonly IMediator _mediator;
    private readonly ILogger<BoundarySubscriptionTriggeredEventHandler> _logger;

    public BoundarySubscriptionTriggeredEventHandler(
        IUnitOfWork uow,
        IMediator mediator,
        ILogger<BoundarySubscriptionTriggeredEventHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(BoundarySubscriptionTriggeredEvent e, CancellationToken ct)
    {


       await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            // 1) Load subscription aggregate (authoritative flags like IsInterrupting/Kind/etc.)
            var sub = await _uow.BoundarySubscriptions.GetByIdAsync(e.SubscriptionId, trxCt);
            if (sub is null)
            {
                _logger.LogWarning(
                    "[BND-TRIGGER] Subscription not found. SubscriptionId={SubscriptionId}",
                    e.SubscriptionId);
                return ;
            }

            
            // 2) Load process + triggering token
            var process = await _uow.Processes.GetByIdAsync(e.ProcessId, trxCt);
            if (process is null)
            {
                _logger.LogWarning("[BND-TRIGGER] Process not found. ProcessId={ProcessId}", e.ProcessId);
                return ;
            }

            var triggeringToken = await _uow.Tokens.GetByIdAsync(e.TokenId, trxCt);
            if (triggeringToken is null)
            {
                _logger.LogWarning("[BND-TRIGGER] Token not found. TokenId={TokenId}", e.TokenId);
                return ;
            }

            // 3) If interrupting: cancel the current activity instance
            //    (tokens + node instances + other subscriptions correlated by ActivityInstanceId)
            if (sub.IsInterrupting && sub.ActivityInstanceId is Guid ai && ai != Guid.Empty)
            {
                // 3.1) cancel other active boundary subscriptions for this activity instance
                var activeSubs = await _uow.BoundarySubscriptions.GetActiveByActivityInstanceAsync(
                    activityInstanceId: ai,
                    trxCt);

                foreach (var s in activeSubs)
                {
                    if (s.Id == sub.Id) continue;
                    s.Cancel("Canceled by interrupting boundary event.");
                    await _uow.BoundarySubscriptions.UpdateAsync(s, trxCt);
                }

                // 3.2) cancel node instances in this activity instance
                var nodesInAi = await _uow.NodeInstances.GetByActivityInstanceIdAsync(
                    processId: process.Id,
                    activityInstanceId: ai,
                    trxCt);

                foreach (var n in nodesInAi)
                {
                    // do not alter already terminal nodes
                    if (n.State is NodeState.Completed or NodeState.Failed or NodeState.Skipped)
                        continue;

                    n.Skip("Interrupted by boundary event.");
                    await _uow.NodeInstances.UpdateAsync(n, trxCt);
                }

                // 3.3) terminate tokens in this activity instance
                var tokensInAi = await _uow.Tokens.GetByActivityInstanceIdAsync(
                    processId: process.Id,
                    activityInstanceId: ai,
                    trxCt);

                foreach (var t in tokensInAi)
                {
                    // skip already terminal
                    if (t.State is TokenState.Completed or TokenState.Terminated)
                        continue;

                    // It's OK to terminate the triggering token too; we'll spawn a new token for boundary path.
                    t.Terminate("Interrupted by boundary event.");
                    await _uow.Tokens.UpdateAsync(t, trxCt);
                }
            }

            // 4) Create a NEW token for boundary path (robust even if triggering token is Failed/Waiting/Terminated)
            //    Boundary flow is outside the host activity => clear ActivityInstanceId
            var boundaryToken = new Token(
                processId: process.Id,
                startElementId: e.BoundaryElementId,
                parentTokenIds: new[] { triggeringToken.Id });

            // inherit correlation scope if you need (optional)
            if (triggeringToken.ScopeId is Guid scope && scope != Guid.Empty)
                boundaryToken.SetScope(scope);

            // boundary token is a new execution path => ensure no activity instance
            if (boundaryToken.ActivityInstanceId is not null)
                boundaryToken.ClearActivityInstance();

            boundaryToken.Activate();
            
            await _uow.Tokens.AddAsync(boundaryToken, trxCt);

            // register token on process aggregate (IDs only)
            process.AddToken(boundaryToken.Id);
            await _uow.Processes.UpdateAsync(process, trxCt);

            _logger.LogInformation(
                "[BND-TRIGGER] Triggered. SubId={SubId} Host={Host}  NewToken={NewToken} NewNode={NewNode} Interrupting={Interrupting}",
                sub.Id, sub.HostElementId, sub.BoundaryElementId, boundaryToken.Id, sub.IsInterrupting);

            return ;
        }, ct);

    }
}
