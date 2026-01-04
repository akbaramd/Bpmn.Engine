using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Gateway handler (production-ready, consistent with your new error model).
///
/// Responsibilities:
/// 1) TokenProcessAsync: JOIN/MERGE only (token-driven)
/// 2) NodeProcessAsync: mirror token state into node, validate outgoing, mark processed+complete
/// 3) TokenNavigateAsync: SPLIT/FORK only + fallback default navigation
///
/// Error model:
/// - Use node.Fail(message, EngineErrorKind) only (NO token.Fail here)
/// - Token is authoritative for terminal state; node mirrors token state defensively.
/// </summary>
public sealed class GatewayHandler : BpmnElementHandlerBase
{
    private readonly IGatewaySplitService _split;
    private readonly IGatewayJoinService _join;
    private readonly ILogger<GatewayHandler> _logger;

    public GatewayHandler(
        IGatewaySplitService split,
        IGatewayJoinService join,
        IFeelExpressionEvaluator feel,
        ILogger<GatewayHandler> logger)
        : base(feel, logger)
    {
        _split = split ?? throw new ArgumentNullException(nameof(split));
        _join = join ?? throw new ArgumentNullException(nameof(join));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override bool CanHandle(BpmnFlowElement element) => element is BpmnGateway;

    // ============================================================
    // TOKEN PROCESS: Join/Merge ONLY (token-driven)
    // ============================================================
    public override async Task<TokenProcessResult> TokenProcessAsync(
        Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (token is null) throw new ArgumentNullException(nameof(token));
        if (element is null) throw new ArgumentNullException(nameof(element));
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        // Terminal safety: gateway join shouldn't touch terminal tokens
        if (token.State is TokenState.Terminated or TokenState.Failed or TokenState.Merged)
            return TokenProcessResult.NoOp;

        var gateway = (BpmnGateway)element;

        try
        {
            // Join first (AND/OR joins, not XOR joins)
            var joinOutcome = await _join.TryJoinAsync(process, token, gateway, ctx, ct).ConfigureAwait(false);

            // If token was merged and is waiting, it is consumed (no further processing)
            if (joinOutcome is GatewayJoinOutcome.ChildMergedAndWaiting or GatewayJoinOutcome.ParentReactivated)
                return TokenProcessResult.Consumed;

            if (joinOutcome == GatewayJoinOutcome.Failed)
                return TokenProcessResult.Failed;

            // Otherwise continue normal node processing
            return TokenProcessResult.Continue;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[GW] Join threw exception. P={ProcessId} T={TokenId} Gw={GwId} El={ElementId}",
                process.Id, token.Id, gateway.id, token.CurrentElementId);

            // Token is the driver here; we report failure to pipeline.
            return TokenProcessResult.Failed;
        }
    }

    // ============================================================
    // NODE PROCESS: Safe (NO join logic)
    // - Mirror token state to node
    // - Validate outgoing
    // - token.Processed()
    // - node.Complete()
    // ============================================================
    public override Task<ElementProcessResult> NodeProcessAsync(
        Process process,
        Token token,
        NodeInstance node,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (token is null) throw new ArgumentNullException(nameof(token));
        if (node is null) throw new ArgumentNullException(nameof(node));
        if (element is null) throw new ArgumentNullException(nameof(element));
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        // Already terminal node => nothing to do
        if (node.State is NodeState.Completed or NodeState.Failed or NodeState.Skipped)
            return Task.FromResult(ElementProcessResult.NoOp);

        // 1) Mirror token terminal states (authoritative)
        if (token.State is TokenState.Terminated)
        {
            node.Complete(); // or Terminate if you add Node.Terminate(...)
            return Task.FromResult(ElementProcessResult.Terminated);
        }

        if (token.State is TokenState.Failed)
        {
            node.Fail("Token is Failed before gateway node processing.", EngineErrorKind.Logical);
            return Task.FromResult(ElementProcessResult.Failed);
        }

        if (token.State is TokenState.Waiting)
        {
            // Join waiting or other pause
            node.WaitForJoin();
            return Task.FromResult(ElementProcessResult.Waiting);
        }

        // 2) Gateway itself shouldn't do IO mapping. It just controls flow.
        //    But we must validate it can route somewhere.
        try
        {
            var outgoing = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);

            if (outgoing is null || outgoing.Count == 0)
            {
                Logger.LogWarning(
                    "[GW] No outgoing flows. P={ProcessId} T={TokenId} ElementId={ElementId}",
                    process.Id, token.Id, token.CurrentElementId);

                node.Fail("Gateway has no outgoing sequence flows.", EngineErrorKind.Logical);
                return Task.FromResult(ElementProcessResult.Failed);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "[GW] Failed to read outgoing flows. P={ProcessId} T={TokenId} ElementId={ElementId}",
                process.Id, token.Id, token.CurrentElementId);

            node.Fail("Gateway outgoing-flow lookup failed.", EngineErrorKind.Technical);
            return Task.FromResult(ElementProcessResult.Failed);
        }

        // 3) Mark processed + complete node
        token.Processed();
        node.Complete();

        return Task.FromResult(ElementProcessResult.Completed);
    }

    // ============================================================
    // TOKEN NAVIGATION: Split/Fork or default route
    // ============================================================
    public override async Task TokenNavigateAsync(
        Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (token is null) throw new ArgumentNullException(nameof(token));
        if (element is null) throw new ArgumentNullException(nameof(element));
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        if (token.State is TokenState.Waiting or TokenState.Terminated or TokenState.Failed)
            return;

        var gateway = (BpmnGateway)element;

        // If model lookup fails, don't crash the worker loop; just stop navigation
        // (the node failure should be handled elsewhere).
        IReadOnlyList<BpmnSequenceFlow>? outgoing;
        try
        {
            outgoing = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[GW:NAV] Outgoing-flow lookup failed. P={ProcessId} T={TokenId} Gw={GwId} ElementId={ElementId}",
                process.Id, token.Id, gateway.id, token.CurrentElementId);
            return;
        }

        if (outgoing is null || outgoing.Count == 0)
            return;

        // Split/Fork only here
        if (outgoing.Count > 1)
        {
            try
            {
                var splitHandled = await _split.TrySplitAsync(process, token, gateway, ctx, ct).ConfigureAwait(false);
                if (splitHandled)
                    return;

                _logger.LogWarning(
                    "[GW:NAV] Split expected but not handled. Falling back. P={ProcessId} T={TokenId} Gw={GwId}",
                    process.Id, token.Id, gateway.id);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[GW:NAV] Split threw exception. P={ProcessId} T={TokenId} Gw={GwId}",
                    process.Id, token.Id, gateway.id);
                return;
            }
        }

        // Fallback to base navigation (single outgoing OR split not handled)
        await base.TokenNavigateAsync(process, token, element, ctx, isResume, ct).ConfigureAwait(false);
    }
}
