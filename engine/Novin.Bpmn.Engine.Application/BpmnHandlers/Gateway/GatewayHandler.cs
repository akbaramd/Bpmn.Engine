using System.Linq;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;
using NodeState = Novin.Bpmn.Engine.Domain.Entities.NodeState;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

public sealed class GatewayHandler : BpmnElementHandlerBase
{
    private readonly IGatewaySplitService _split;
    private readonly IGatewayJoinService _join;
    private readonly IVariableMappingService _variableMapping;
    private readonly ITokenRepository _tokenRepository;
    private readonly INodeInstanceRepository _nodeRepository;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<GatewayHandler> _logger;


    public GatewayHandler(
        IGatewaySplitService split,
                 IGatewayJoinService join,
        IVariableMappingService variableMapping,
        IFeelExpressionEvaluator feel,
        ILogger<GatewayHandler> logger,
        ITokenRepository tokenRepository,
        INodeInstanceRepository nodeRepository,
        IUnitOfWork uow)
        : base(feel, logger)
    {
        _split = split ?? throw new ArgumentNullException(nameof(split));
        _join = join ?? throw new ArgumentNullException(nameof(join));
        _variableMapping = variableMapping ?? throw new ArgumentNullException(nameof(variableMapping));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenRepository = tokenRepository ?? throw new ArgumentNullException(nameof(tokenRepository));
        _nodeRepository = nodeRepository ?? throw new ArgumentNullException(nameof(nodeRepository));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    public override bool CanHandle(BpmnFlowElement element) => element is BpmnGateway;

    // ============================================================
    // TOKEN PROCESS: Join/Merge ONLY (token-driven, no node usage)
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

        // terminal safety
        if (token.State is TokenState.Terminated or TokenState.Failed or TokenState.Merged)
            return TokenProcessResult.NoOp;

        var gateway = (BpmnGateway)element;

        // Try join first (only AND/OR join, not XOR)
        var joinOutcome = await _join.TryJoinAsync(process, token, gateway, ctx, ct);

        if (joinOutcome is GatewayJoinOutcome.ChildMergedAndWaiting or GatewayJoinOutcome.ParentReactivated)
            return TokenProcessResult.Consumed;

        if (joinOutcome == GatewayJoinOutcome.Failed)
            return TokenProcessResult.Failed;

        // Otherwise continue normal processing
        return TokenProcessResult.Continue;
    }


    // ============================================================
    // NODE PROCESS: Safe (NO join logic)
    // - IO mapping (inputs/outputs)
    // - token.Processed()
    // - node.Complete/Fail/Wait derived from token state
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
        // If token already terminal or waiting, node should follow it.
        if (token.State is TokenState.Terminated)
        {
            node.Complete(); // or node.Terminate(...) if you have it
            return Task.FromResult(ElementProcessResult.Terminated);
        }

        if (token.State is TokenState.Failed)
        {
            node.Fail("Token failed before node processing.");
            return Task.FromResult(ElementProcessResult.Failed);
        }

        if (token.State is TokenState.Waiting)
        {
            // join waiting or other pause
            // (adjust signature if your NodeInstance.Wait() differs)
            node.WaitForJoin();
            return Task.FromResult(ElementProcessResult.Waiting);
        }

        var outgoing = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);

        if (outgoing.Count == 0)
        {
            Logger.LogWarning("[GW] No outgoing flows. ElementId={ElementId}", token.CurrentElementId);
            node.Fail("Gateway has no outgoing sequence flows.");
            token.Fail("Gateway has no outgoing sequence flows.");
            return Task.FromResult(ElementProcessResult.Failed);
        }

        // ⛔ Gateway does NOT perform variable mapping (only state/control + join variables)
        // Mark processed + complete node
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
        if (token.State is TokenState.Waiting or TokenState.Terminated or TokenState.Failed)
            return;

        var gateway = (BpmnGateway)element;
        var outgoing = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);
        if (outgoing is null || outgoing.Count == 0)
            return;

        // Split/Fork only here
        if (outgoing.Count > 1)
        {
            var splitHandled = await _split.TrySplitAsync(process, token, gateway, ctx, ct);
            if (splitHandled)
                return;

            _logger.LogWarning(
                "[GW:NAV] Split expected but not handled. Falling back to default navigation. Gw={Gw}",
                gateway.id);
        }

        // Fallback to base navigation (single outgoing OR split not handled)
        await base.TokenNavigateAsync(process, token, element, ctx, isResume, ct);
    }

  
}