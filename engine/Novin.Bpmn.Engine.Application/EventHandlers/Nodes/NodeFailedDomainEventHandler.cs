using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Production-ready handler for NodeFailedDomainEvent:
/// - Mirrors node failure into Token failure (best-effort, guarded)
/// - Triggers matching Error Boundary subscriptions with a clear, testable matching policy
/// - Keeps all DB mutations inside ONE transaction
///
/// Matching rules (BPMN ErrorCode semantics):
/// - If event ErrorCode is NULL/empty => only "catch-all" subscriptions (sub.ErrorCode null/empty) match
/// - If event ErrorCode is NOT empty:
///     - exact match subscriptions match
///     - optionally catch-all can also match (policy flag)
///
/// ErrorKind semantics:
/// - EngineErrorKind.Technical / Logical: NOT catchable by BPMN Error boundary (normally)
/// - EngineErrorKind.BpmnError: catchable by Error boundary, and uses ErrorCode matching above
/// </summary>
public sealed class NodeFailedDomainEventHandler : INotificationHandler<NodeFailedDomainEvent>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<NodeFailedDomainEventHandler> _logger;
    private readonly BoundaryErrorMatchPolicy _policy;

    public NodeFailedDomainEventHandler(
        IUnitOfWork uow,
        ILogger<NodeFailedDomainEventHandler> logger,
        BoundaryErrorMatchPolicy? policy = null)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _policy = policy ?? BoundaryErrorMatchPolicy.Default;
    }

    public async Task Handle(NodeFailedDomainEvent e, CancellationToken ct)
    {
        _logger.LogError(
            "[NODE-FAILED] NodeId={NodeId} TokenId={TokenId} ElementId={ElementId}  ErrorCode={ErrorCode} Error={Error}",
            e.NodeId, e.TokenId, e.ElementId,  e.ErrorCode, e.ErrorMessage);

        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            // 1) Load aggregates (tracked)
            var node = await _uow.NodeInstances.GetByIdAsync(e.NodeId, trxCt);
            if (node is null)
            {
                _logger.LogWarning("[NODE-FAILED] Node not found. NodeId={NodeId}", e.NodeId);
                return;
            }

            var process = await _uow.Processes.GetByIdAsync(e.ProcessId, trxCt);
            if (process is null)
            {
                _logger.LogWarning("[NODE-FAILED] Process not found. ProcessId={ProcessId}", e.ProcessId);
                return;
            }

            var token = await _uow.Tokens.GetByIdAsync(e.TokenId, trxCt);
            if (token is null)
            {
                _logger.LogWarning("[NODE-FAILED] Token not found. TokenId={TokenId}", e.TokenId);
                return;
            }

            // 2) Mirror to token (ALWAYS, best-effort + guarded)
            MirrorFailureToTokenBestEffort(token, e);

       
            // 4) If we can't correlate boundary subscriptions, stop safely
            if (node.ActivityInstanceId is null)
            {
                _logger.LogWarning(
                    "[NODE-FAILED] ActivityInstanceId is null. Cannot match boundary subscriptions. NodeId={NodeId}",
                    node.Id);
                return;
            }

            // 5) Get active error boundary subscriptions (repo may already filter by Kind==Error)
            var subs = await _uow.BoundarySubscriptions.GetActiveErrorSubscriptionsByErrorCodeAsync(
                processId: process.Id,
                nodeInstanceId: node.Id,
                activityInstanceId: node.ActivityInstanceId.Value,
                ct: trxCt);

            if (subs is null) return;

            var list = subs as IList<BoundaryEventSubscription> ?? subs.ToList();
            if (list.Count == 0) return;

            // 6) Match + trigger
            var matched = 0;

            foreach (var sub in list)
            {
                if (sub.Kind != BoundaryKind.Error)
                    continue;

                if (!string.IsNullOrWhiteSpace(sub.ErrorCode) && !BoundaryErrorMatcher.IsMatch(e.ErrorCode, sub.ErrorCode, _policy))
                    continue;

                matched++;

                sub.MarkTriggered(reason: BuildReason(e));
                await _uow.BoundarySubscriptions.UpdateAsync(sub, trxCt);
            }

            _logger.LogInformation(
                "[NODE-FAILED] Boundary matched={Matched} total={Total} NodeId={NodeId} ErrorCode={ErrorCode} Policy={Policy}",
                matched, list.Count, e.NodeId, e.ErrorCode, _policy);
        }, ct);
    }

    private void MirrorFailureToTokenBestEffort(Token token, NodeFailedDomainEvent e)
    {
        // ✅ Guard: don't fail terminal tokens (prevents crashes / re-fail bugs)
        if (token.State is TokenState.Terminated or TokenState.Completed or TokenState.Failed)
        {
            _logger.LogDebug(
                "[NODE-FAILED] Skip token fail: token is terminal. TokenId={TokenId} State={State}",
                token.Id, token.State);
            return;
        }

        // Keep error formatting consistent, and keep BPMN ErrorCode separately when available.
        var msg = string.IsNullOrWhiteSpace(e.ErrorCode.ToString())
            ? e.ErrorMessage
            : $"[{e.ErrorCode}] {e.ErrorMessage}";

        try
        {
            // Your new signature supports kind + optional errorCode.
            // We only pass errorCode when it's a BPMN error (so boundaries can match by code).
            token.Fail(
                error: msg,
                e.ErrorCode,
                errorCode: e.ErrorCode == EngineErrorKind.BpmnError ? e.ErrorCode.ToString() : null);
        }
        catch (Exception ex)
        {
            // Do NOT break boundary triggering / transaction for token fail issues.
            _logger.LogWarning(ex,
                "[NODE-FAILED] Token.Fail threw. TokenId={TokenId} State={State}",
                token.Id, token.State);
        }
    }

    private static string BuildReason(NodeFailedDomainEvent e)
        => string.IsNullOrWhiteSpace(e.ErrorCode.ToString())
            ? $"Node failed (global): {e.ErrorMessage}"
            : $"Node failed (code={e.ErrorCode}): {e.ErrorMessage}";
}

/// <summary>
/// Testable matcher for BPMN Error boundary subscriptions (string error codes).
/// </summary>
public static class BoundaryErrorMatcher
{
    public static bool IsMatch(
        EngineErrorKind? engineError,
        string? subscriptionErrorCode,
        BoundaryErrorMatchPolicy policy)
    {
        var eventErrorCode = engineError.ToString();
        if (policy is null) throw new ArgumentNullException(nameof(policy));

        var evHasCode = !string.IsNullOrWhiteSpace(eventErrorCode);
        var subHasCode = !string.IsNullOrWhiteSpace(subscriptionErrorCode);

        // Event has no code => only catch-all subscriptions match
        if (!evHasCode)
            return !subHasCode;

        // Event has code:
        // 1) exact match
        if (subHasCode && string.Equals(subscriptionErrorCode, eventErrorCode, policy.Comparison))
            return true;

        // 2) optional: allow catch-all even when event has code
        if (policy.TriggerCatchAllWhenCodePresent && !subHasCode)
            return true;

        return false;
    }
}

/// <summary>
/// Match policy:
/// - TriggerCatchAllWhenCodePresent=false => strict (prefer explicit code boundaries)
/// - Comparison defaults to Ordinal
/// </summary>
public sealed record BoundaryErrorMatchPolicy(
    bool TriggerCatchAllWhenCodePresent,
    StringComparison Comparison)
{
    public static readonly BoundaryErrorMatchPolicy Default =
        new(TriggerCatchAllWhenCodePresent: false, Comparison: StringComparison.Ordinal);
}
