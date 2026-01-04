using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public enum GatewayJoinOutcome
{
    NotAJoinCandidate,
    ChildMergedAndWaiting,
    ParentReactivated,
    Failed
}

public interface IGatewayJoinService
{
    Task<GatewayJoinOutcome> TryJoinAsync(
        Process process,
        Token token,
        BpmnGateway gateway,
        BpmnRuntimeContext ctx,
        CancellationToken ct);
}

/// <summary>
/// Zeebe-inspired barrier join/merge for structured models:
/// - XOR gateway is pass-through (not a barrier).
/// - Parallel join waits strictly for all forked branches (expectedCount from split, fallback ok).
/// - Inclusive join waits for:
///     - expectedCount (if available), otherwise
///     - "can still reach?" heuristic (if expectedCount missing).
/// - Correlation: (current ScopeId) + ParentTokenId.
/// - IMPORTANT: parent token must still have the SAME current ScopeId on top (scope-stack),
///   otherwise this is a correlation bug.
/// </summary>
public sealed class GatewayJoinService : IGatewayJoinService
{
    private readonly ITokenRepository _tokenRepository;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<GatewayJoinService> _logger;

    public GatewayJoinService(
        ITokenRepository tokenRepository,
        IUnitOfWork uow,
        ILogger<GatewayJoinService> logger)
    {
        _tokenRepository = tokenRepository ?? throw new ArgumentNullException(nameof(tokenRepository));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GatewayJoinOutcome> TryJoinAsync(
        Process process,
        Token token,
        BpmnGateway gateway,
        BpmnRuntimeContext ctx,
        CancellationToken ct)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (token is null) throw new ArgumentNullException(nameof(token));
        if (gateway is null) throw new ArgumentNullException(nameof(gateway));
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        var gwId = gateway.id ?? throw new InvalidOperationException("Gateway must have id.");

        // ---- fast skip reasons (logged) ----
        if (token.State is TokenState.Terminated or TokenState.Failed or TokenState.Merged)
            return Skip("TokenTerminalOrMerged", process, token, gwId, gateway);

        if (!string.Equals(token.CurrentElementId, gwId, StringComparison.Ordinal))
            return Skip("NotAtGateway", process, token, gwId, gateway);

        if (gateway is BpmnExclusiveGateway)
            return Skip("XorPassThrough", process, token, gwId, gateway);

        var isParallelJoin = gateway is BpmnParallelGateway;
        var isInclusiveJoin = gateway is BpmnInclusiveGateway;
        if (!isParallelJoin && !isInclusiveJoin)
            return Skip("NotAndOrGateway", process, token, gwId, gateway);

        // Join barrier only if incoming > 1
        var incoming = ctx.Model.GetIncomingSequenceFlows(ctx.BpmnProcessId, gwId);
        if (incoming is null || incoming.Count <= 1)
            return Skip("IncomingNotBarrier", process, token, gwId, gateway);

        // must be correlated fork-branch via scope + parent
        if (token.ScopeId is null || token.ScopeId == Guid.Empty)
            return Skip("NoScope", process, token, gwId, gateway);

        if (token.ParentTokenId is null || token.ParentTokenId == Guid.Empty)
            return Skip("NoParent", process, token, gwId, gateway);

        var scopeId = token.ScopeId.Value;
        var parentTokenId = token.ParentTokenId.Value;

        // Load parent
        var parent = await _tokenRepository.GetByIdAsync(parentTokenId, ct);
        if (parent is null)
        {
            token.Fail($"Join gateway '{gwId}': parent token not found. ParentTokenId={parentTokenId}",EngineErrorKind.Technical);
            await _uow.Tokens.UpdateAsync(token, ct);
            return Fail("ParentNotFound", process, token, gwId, gateway, scopeId, parentTokenId);
        }

        // ✅ CRITICAL GUARD (fixes weird merges):
        // parent must still have this scope as its CURRENT scope (top of stack).
        if (parent.ScopeId != scopeId)
        {
            token.Fail($"Join gateway '{gwId}': parent scope mismatch. ChildScope={scopeId:N} ParentScope={parent.ScopeId?.ToString("N") ?? "(null)"}", EngineErrorKind.Technical);
            await _uow.Tokens.UpdateAsync(token, ct);

            _logger.LogError(
                "[JOIN:CFG] Parent scope mismatch. Gw={Gw} Scope={Scope} Parent={Parent} ParentScope={ParentScope} Child={Child}",
                gwId, scopeId, parentTokenId, parent.ScopeId, token.Id);

            return GatewayJoinOutcome.Failed;
        }

        // ---- expected count ----
        var expectedCount = ResolveExpectedCount(process, gateway, scopeId, fallbackForParallel: incoming.Count);

        // ---- merge current token (idempotent-ish) ----
        if (token.State != TokenState.Merged)
        {
            token.Merge(parentTokenId, $"Merged at join gateway '{gwId}'.");
            await _uow.Tokens.UpdateAsync(token, ct);
        }

        // ---- count merged children in this scope ----
        var children = (await _tokenRepository.GetChildTokensAsync(parentTokenId, ct)).ToArray();

        var mergedCount = 0;
        var mergedChildren = new List<Token>(capacity: children.Length);

        for (var i = 0; i < children.Length; i++)
        {
            var t = children[i];
            if (t.ScopeId == scopeId && t.State == TokenState.Merged)
            {
                mergedCount++;
                mergedChildren.Add(t);
            }
        }

        _logger.LogInformation(
            "[JOIN] Gw={Gw} Type={Type} Proc={Proc} Scope={Scope} Parent={Parent} Child={Child} " +
            "Merged={Merged} Expected={Expected} ParentState={ParentState}",
            gwId, gateway.GetType().Name, process.Id, scopeId, parentTokenId, token.Id,
            mergedCount, expectedCount, parent.State);

        // ---- completion rules ----

        if (gateway is BpmnParallelGateway)
        {
            if (expectedCount <= 0)
                return Fail("ParallelExpectedCountInvalid", process, token, gwId, gateway, scopeId, parentTokenId);

            if (mergedCount < expectedCount)
                return GatewayJoinOutcome.ChildMergedAndWaiting;

            return await CompleteJoinAsync(process, parent, gwId, scopeId, parentTokenId, mergedCount, expectedCount, mergedChildren, ct);
        }

        // Inclusive join (OR)
        if (expectedCount > 0)
        {
            // structured: expectedCount is the number of branches actually created at split
            if (mergedCount < expectedCount)
                return GatewayJoinOutcome.ChildMergedAndWaiting;

            return await CompleteJoinAsync(process, parent, gwId, scopeId, parentTokenId, mergedCount, expectedCount, mergedChildren, ct);
        }

        // expectedCount missing => heuristic: "can still reach?"
        var canStillReach = HasPotentialArrivals(children, scopeId, gwId);

        _logger.LogDebug(
            "[JOIN:OR] ExpectedCountMissing => CanStillReach={CanStillReach} Gw={Gw} Scope={Scope} Parent={Parent} Merged={Merged}",
            canStillReach, gwId, scopeId, parentTokenId, mergedCount);

        if (canStillReach)
            return GatewayJoinOutcome.ChildMergedAndWaiting;

        // Nobody else can reach => complete with what we have
        return await CompleteJoinAsync(process, parent, gwId, scopeId, parentTokenId, mergedCount, mergedCount, mergedChildren, ct);
    }

    /// <summary>
    /// Heuristic: Any sibling token in same scope which is not terminal and not merged
    /// is considered a potential future arrival.
    /// </summary>
    private static bool HasPotentialArrivals(Token[] children, Guid scopeId, string joinGwId)
    {
        for (var i = 0; i < children.Length; i++)
        {
            var t = children[i];
            if (t.ScopeId != scopeId) continue;

            if (t.State is TokenState.Created or TokenState.Active or TokenState.Waiting)
            {
                // even if it's already at the join gateway but not merged yet, it is still a pending arrival
                return true;
            }
        }
        return false;
    }

    private int ResolveExpectedCount(Process process, BpmnGateway gateway, Guid scopeId, int fallbackForParallel)
    {
        var expectedKey = JoinCorrelationMetaKeys.ExpectedCount(scopeId);

        if (process.TryGetMetadata<string>(expectedKey, out var raw) &&
            !string.IsNullOrWhiteSpace(raw) &&
            int.TryParse(raw, out var n) &&
            n > 0)
            return n;

        // Debug-only info
        var splitGwKey = JoinCorrelationMetaKeys.SplitGatewayId(scopeId);
        if (process.TryGetMetadata<string>(splitGwKey, out var splitGw) && !string.IsNullOrWhiteSpace(splitGw))
        {
            _logger.LogDebug(
                "[JOIN:CFG] expectedCount missing. Scope={Scope} SplitGw={SplitGw}",
                scopeId, splitGw);
        }

        if (gateway is BpmnParallelGateway)
        {
            _logger.LogWarning(
                "[JOIN:CFG] AND join expectedCount missing => fallback to incoming count. Scope={Scope} Incoming={Incoming}",
                scopeId, fallbackForParallel);
            return fallbackForParallel;
        }

        // Inclusive: allow missing and handle via heuristic
        _logger.LogWarning(
            "[JOIN:CFG] OR join expectedCount missing. Scope={Scope} Key={Key} (using can-still-reach heuristic)",
            scopeId, expectedKey);
        return 0;
    }

    private async Task<GatewayJoinOutcome> CompleteJoinAsync(
        Process process,
        Token parent,
        string gwId,
        Guid scopeId,
        Guid parentTokenId,
        int mergedCount,
        int expectedCountUsed,
        List<Token> mergedChildren,
        CancellationToken ct)
    {
        // Reactivate parent only once
        if (parent.State == TokenState.Forked)
        {
            parent.ReactivateFromForked(mergedCount, $"Join completed at '{gwId}'.");

            // ✅ CRITICAL: close ONLY this scope (nested joins need outer scope preserved)
            parent.PopScope(); // <-- replaces ClearScope()

            var arrivedIncomingFlowIds = CollectArrivedIncomingFlows(mergedChildren);

            // Move parent onto join gateway; skipProcess so next tick routes/splits
            parent.MoveTo(gwId, skipProcess: true, arrivedIncomingFlowIds);

            await _uow.Tokens.UpdateAsync(parent, ct);

            CleanupJoinMetadata(process, scopeId);
            await _uow.Processes.UpdateAsync(process, ct);

            _logger.LogInformation(
                "[JOIN:COMPLETE] Gw={Gw} Scope={Scope} Parent={Parent} Merged={Merged}/{ExpectedUsed} NewParentScope={NewScope}",
                gwId, scopeId, parentTokenId, mergedCount, expectedCountUsed, parent.ScopeId);

            return GatewayJoinOutcome.ParentReactivated;
        }

        _logger.LogDebug(
            "[JOIN] Parent already reactivated (concurrent). Gw={Gw} Scope={Scope} Parent={Parent} ParentState={State}",
            gwId, scopeId, parentTokenId, parent.State);

        return GatewayJoinOutcome.ParentReactivated;
    }

    private static void CleanupJoinMetadata(Process process, Guid scopeId)
    {
        process.RemoveMetadata(JoinCorrelationMetaKeys.SplitGatewayId(scopeId));
        process.RemoveMetadata(JoinCorrelationMetaKeys.SplitGatewayType(scopeId));
        process.RemoveMetadata(JoinCorrelationMetaKeys.ExpectedCount(scopeId));
        process.RemoveMetadata(JoinCorrelationMetaKeys.Branches(scopeId));
    }

    private static string?[] CollectArrivedIncomingFlows(List<Token> mergedChildren)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < mergedChildren.Count; i++)
        {
            var flows = mergedChildren[i].ArrivedViaFlowIds;
            if (flows is null) continue;

            for (var j = 0; j < flows.Count; j++)
            {
                var f = flows[j];
                if (!string.IsNullOrWhiteSpace(f))
                    set.Add(f!);
            }
        }

        if (set.Count == 0) return Array.Empty<string?>();

        var arr = new string?[set.Count];
        var k = 0;
        foreach (var x in set) arr[k++] = x;
        return arr;
    }

    private GatewayJoinOutcome Skip(string reason, Process process, Token token, string gwId, BpmnGateway gateway)
    {
        _logger.LogDebug(
            "[JOIN:SKIP] Reason={Reason} Gw={Gw} Type={Type} Proc={Proc} Token={Token} El={El} State={State} Scope={Scope} Parent={Parent}",
            reason, gwId, gateway.GetType().Name, process.Id, token.Id, token.CurrentElementId, token.State, token.ScopeId, token.ParentTokenId);

        return GatewayJoinOutcome.NotAJoinCandidate;
    }

    private GatewayJoinOutcome Fail(string reason, Process process, Token token, string gwId, BpmnGateway gateway, Guid scopeId, Guid parentTokenId)
    {
        var key = JoinCorrelationMetaKeys.ExpectedCount(scopeId);
        var val = process.TryGetMetadata<string>(key, out var v) ? v : "(missing)";

        _logger.LogError(
            "[JOIN:FAIL] Reason={Reason} Gw={Gw} Type={Type} Proc={Proc} Token={Token} El={El} State={State} Scope={Scope} Parent={Parent} MetaExpectedKey={Key} MetaExpectedVal={Val}",
            reason, gwId, gateway.GetType().Name, process.Id, token.Id, token.CurrentElementId, token.State, scopeId, parentTokenId, key, val);

        return GatewayJoinOutcome.Failed;
    }
}
