using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Commands.CreateToken;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using NodeState = Novin.Bpmn.Engine.Domain.Entities.NodeState;
using Novin.Bpmn.Engine.Application.Services;
using System.Text.Json.Nodes;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Handles a triggered BoundaryEventSubscription (Zeebe-inspired, structured-safe):
/// - Interrupting: cancels tokens/nodes/subscriptions in the SAME ActivityInstanceId (host activity instance),
///   then spawns a boundary token.
/// - Non-interrupting: spawns a boundary token WITHOUT joining the parent fork/join correlation (detach),
///   unless explicitly configured to participate.
/// - Guards against poison correlation states (scope without parent or parent without scope).
/// - Clones variables to avoid shared reference bugs.
/// </summary>
public sealed class BoundarySubscriptionTriggeredEventHandler
    : INotificationHandler<BoundarySubscriptionTriggeredEvent>
{
    private readonly IUnitOfWork _uow;
    private readonly IMediator _mediator;
    private readonly ILogger<BoundarySubscriptionTriggeredEventHandler> _logger;

    // ✅ Structured default (recommended): non-interrupting boundaries do NOT participate in join correlation
    // If you *really* want join-participating non-interrupting boundaries, set this to true
    // (and ensure your model semantics match it).
    private const bool NonInterruptingParticipatesInJoin = false;

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
            // 1) Subscription (authoritative flags)
            var sub = await _uow.BoundarySubscriptions.GetByIdAsync(e.SubscriptionId, trxCt);
            if (sub is null)
            {
                _logger.LogWarning("[BND-TRIGGER] Subscription not found. SubscriptionId={SubscriptionId}", e.SubscriptionId);
                return;
            }

            // 2) Process + triggering token
            var process = await _uow.Processes.GetByIdAsync(e.ProcessId, trxCt);
            if (process is null)
            {
                _logger.LogWarning("[BND-TRIGGER] Process not found. ProcessId={ProcessId}", e.ProcessId);
                return;
            }

            var triggeringToken = await _uow.Tokens.GetByIdAsync(e.TokenId, trxCt);
            if (triggeringToken is null)
            {
                _logger.LogWarning("[BND-TRIGGER] Token not found. TokenId={TokenId}", e.TokenId);
                return;
            }

            // 3) Interrupting cancel scope = ActivityInstanceId of host activity instance
            if (sub.IsInterrupting && sub.ActivityInstanceId is Guid ai && ai != Guid.Empty)
            {
                // 3.1) cancel other active subscriptions in same activity instance
                var activeSubs = await _uow.BoundarySubscriptions.GetActiveByActivityInstanceAsync(ai, trxCt);
                foreach (var s in activeSubs)
                {
                    if (s.Id == sub.Id) continue;
                    s.Cancel("Canceled by interrupting boundary event.");
                    await _uow.BoundarySubscriptions.UpdateAsync(s, trxCt);
                }

                // 3.2) skip node instances in same activity instance
                var nodesInAi = await _uow.NodeInstances.GetByActivityInstanceIdAsync(process.Id, ai, trxCt);
                foreach (var n in nodesInAi)
                {
                    if (n.State is NodeState.Completed or NodeState.Failed or NodeState.Skipped)
                        continue;

                    n.Skip("Interrupted by boundary event.");
                    await _uow.NodeInstances.UpdateAsync(n, trxCt);
                }

                // 3.3) terminate tokens in same activity instance
                var tokensInAi = await _uow.Tokens.GetByActivityInstanceIdAsync(process.Id, ai, trxCt);
                foreach (var t in tokensInAi)
                {
                    if (t.State is TokenState.Completed or TokenState.Terminated)
                        continue;

                    t.Terminate("Interrupted by boundary event.");
                    await _uow.Tokens.UpdateAsync(t, trxCt);
                }
            }

            // 4) Arrived flow (best-effort)
            var arrivedViaFlowId = triggeringToken.ArrivedViaFlowIds?.LastOrDefault();

            // 5) ✅ Clone variables (avoid sharing reference across tokens)
            Dictionary<string, JsonNode?>? varsCopy = null;
            if (triggeringToken.Variables is not null && triggeringToken.Variables.Count > 0)
            {
                varsCopy = new Dictionary<string, JsonNode?>(triggeringToken.Variables, StringComparer.Ordinal);
            }

            // 6) Correlation decision (Parent/Scope/ScopeStack)
            var (parentId, scopeId, scopeStackSnapshot) = ResolveBoundaryCorrelation(sub, triggeringToken);

            // 7) If policy is join-participating non-interrupting => expectedCount++
            if (!sub.IsInterrupting && NonInterruptingParticipatesInJoin && scopeId.HasValue && parentId.HasValue)
            {
                var key = JoinCorrelationMetaKeys.ExpectedCount(scopeId.Value);

                if (process.TryGetMetadata<string>(key, out var raw) &&
                    !string.IsNullOrWhiteSpace(raw) &&
                    int.TryParse(raw, out var n) &&
                    n > 0)
                {
                    process.SetMetadata(key, (n + 1).ToString());
                    await _uow.Processes.UpdateAsync(process, trxCt);

                    _logger.LogInformation(
                        "[BND:JOIN] Non-interrupting boundary contributes to join => expectedCount++. Scope={Scope} {Old}->{New}",
                        scopeId.Value, n, n + 1);
                }
                else
                {
                    // If we cannot safely increment expectedCount, we MUST detach correlation to avoid deadlocks at joins.
                    _logger.LogError(
                        "[BND:JOIN] expectedCount missing/invalid => detaching boundary from join. Scope={Scope} Key={Key} Val={Val}",
                        scopeId.Value, key, raw ?? "(missing)");

                    parentId = null;
                    scopeId = null;
                    scopeStackSnapshot = null;
                }
            }

            // 8) Create boundary token
            var createResult = await _mediator.Send(
                new CreateTokenCommand(
                    ProcessId: process.Id,
                    StartElementId: e.BoundaryElementId,
                    ParentTokenId: parentId,
                    ArrivedViaFlowId: arrivedViaFlowId,
                    ScopeId: scopeId,                     // backward compatibility (handler should prefer stack)
                    Variables: varsCopy,
                    ScopeStackSnapshot: scopeStackSnapshot // ✅ if your CreateTokenCommand supports it
                ),
                trxCt);

            if (!createResult.Success)
            {
                _logger.LogError(
                    "[BND-TRIGGER] Failed to create boundary token. Proc={Proc} Sub={Sub} Host={Host} Boundary={Boundary} Error={Error}",
                    process.Id, sub.Id, sub.HostElementId, sub.BoundaryElementId, createResult.Error);

                // In production, don't throw unless you want to rollback cancels. Usually rollback is desirable,
                // but depends on your incident/retry model. We'll throw to keep atomicity.
                throw new InvalidOperationException(createResult.Error ?? "Failed to create boundary token.");
            }

            _logger.LogInformation(
                "[BND-TRIGGER] Triggered. SubId={SubId} Host={Host} Boundary={Boundary} NewToken={NewToken} Interrupting={Interrupting} Parent={Parent} Scope={Scope} Depth={Depth}",
                sub.Id, sub.HostElementId, sub.BoundaryElementId, createResult.TokenId, sub.IsInterrupting,
                parentId, scopeId, scopeStackSnapshot?.Length ?? 0);
        }, ct);
    }

    /// <summary>
    /// Zeebe-inspired / structured-safe correlation rules:
    /// - Interrupting: boundary replaces current activity execution => keep join correlation ONLY if complete.
    /// - Non-interrupting:
    ///   - default structured: detach (no parent/scope) to avoid mixing scopes at downstream joins
    ///   - optional: if you explicitly choose join-participating, keep correlation only if complete.
    /// 
    /// Also protects against poison states: scope without parent or parent without scope.
    /// </summary>
    private (Guid? parentTokenId, Guid? scopeId, Guid[]? scopeStackSnapshot) ResolveBoundaryCorrelation(
        BoundaryEventSubscription sub,
        Token triggeringToken)
    {
        // Default structured behavior: detach non-interrupting boundaries
        if (!sub.IsInterrupting && !NonInterruptingParticipatesInJoin)
            return (null, null, null);

        var p = triggeringToken.ParentTokenId;
        var s = triggeringToken.ScopeId;

        var hasParent = p.HasValue && p.Value != Guid.Empty;
        var hasScope = s.HasValue && s.Value != Guid.Empty;

        // Complete correlation => keep + pass scope stack snapshot if available
        if (hasParent && hasScope)
        {
            Guid[]? stack = null;
            if (triggeringToken.ScopeStack is not null && triggeringToken.ScopeStack.Count > 0)
                stack = triggeringToken.ScopeStack.Where(x => x != Guid.Empty).ToArray();

            // if stack absent, CreateTokenHandler can still PushScope(scopeId) when parent exists.
            return (p, s, stack);
        }

        // Poison correlation: log + detach
        if (hasParent || hasScope)
        {
            _logger.LogWarning(
                "[BND:SCOPE] Incomplete correlation => detached. " +
                "SubId={SubId} Token={Token} Interrupting={Interrupting} Parent={Parent} Scope={Scope}",
                sub.Id, triggeringToken.Id, sub.IsInterrupting, p, s);
        }

        return (null, null, null);
    }
}
