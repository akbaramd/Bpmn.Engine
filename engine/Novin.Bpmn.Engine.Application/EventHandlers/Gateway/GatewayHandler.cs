using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.MoveToken;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

public sealed class GatewayHandler : BpmnElementHandlerBase
{
    private readonly IGatewayJoinService _join;
    private readonly IGatewaySplitService _split;
    private readonly IVariableMappingService _variableMapping;
    private readonly ILogger<GatewayHandler> _logger;

    public GatewayHandler(
        IGatewayJoinService join,
        IGatewaySplitService split,
        IVariableMappingService variableMapping,
        IMediator mediator,
        IFeelExpressionEvaluator feel,
        ILogger<GatewayHandler> logger)
        : base(mediator, feel, logger)
    {
        _join = join ?? throw new ArgumentNullException(nameof(join));
        _split = split ?? throw new ArgumentNullException(nameof(split));
        _variableMapping = variableMapping ?? throw new ArgumentNullException(nameof(variableMapping));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override bool CanHandle(BpmnFlowElement element) => element is BpmnGateway;

    /// <summary>
    /// ProcessAsync: فقط merge/join + IO mapping + CompleteTokenCommand
    /// هیچ MoveToken و هیچ Split اینجا انجام نمی‌شود.
    /// </summary>
    public override async Task<ElementProcessResult> ProcessAsync(
        Domain.Entities.Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        var gateway = (BpmnGateway)element;

        var incoming = ctx.Model.GetIncomingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);
        var outgoing = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);

        using (_logger.BeginScope(new Dictionary<string, string?>
        {
            ["ProcessId"] = process.Id.ToString(),
            ["TokenId"] = token.Id.ToString(),
            ["GatewayId"] = gateway.id,
            ["GatewayType"] = gateway.GetType().Name,
            ["ElementId"] = token.CurrentElementId,
            ["State"] = token.State.ToString(),
            ["ScopeId"] = token.ScopeId?.ToString(),
            ["ArrivedVia"] = token.ArrivedViaFlowId,
            ["Exec"] = token.IsExecutable.ToString(),
            ["InCnt"] = incoming.Count.ToString(),
            ["OutCnt"] = outgoing.Count.ToString(),
        }))
        {
            // Guard
            if (token.State is TokenState.Terminated or TokenState.Failed)
                return ElementProcessResult.NoOp;

            // 1) Input mapping فقط executable و فقط بار اول
            if (token.IsExecutable && !isResume)
            {
                token.ClearLocalVariables();
                _variableMapping.ApplyInputs(process, token, element, ctx);
            }

            // 2) Join/Merge (فقط اینجا)
            // اگر join انجام شد (wait/merge/consume-late-arrival)، همینجا تمام.
            var handledByJoin = await _join.TryJoinAsync(process, token, gateway, ctx, ct);
            if (handledByJoin)
            {
                // join-service ممکن است توکن را Waiting کند، merge کند، یا fail کند
                // در هر دو حالت، این handler دیگر CompleteToken نمی‌زند
                if (token.State == TokenState.Waiting)
                    return ElementProcessResult.Waiting;
                if (token.State == TokenState.Failed)
                    return ElementProcessResult.Completed; // token failed, no further processing
                return ElementProcessResult.Completed;
            }

            if (outgoing.Count == 0)
            {
                _logger.LogWarning("[GW] No outgoing flows. ElementId={ElementId}", token.CurrentElementId);
                // سیاست شما: Fail یا Terminate؟
                token.Fail("Gateway has no outgoing sequence flows.");
                return ElementProcessResult.Completed;
            }

            // 3) Output mapping فقط زمانی که split نیست (outgoing<=1)
            // split در NavigateAsync انجام می‌شود.
            if (token.IsExecutable && outgoing.Count <= 1)
                _variableMapping.ApplyOutputs(process, token, element, ctx);

            // 4) Mark token as processed (NodeDone)
            // This publishes TokenProcessedEvent which triggers navigation
            token.Processed();

            return ElementProcessResult.Completed;
        }
    }

    /// <summary>
    /// NavigateAsync: فقط split/fork یا move
    /// - اگر outgoing>1 => split service
    /// - اگر outgoing==1 => move
    /// </summary>
    public override async System.Threading.Tasks.Task NavigateAsync(
        Domain.Entities.Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        // اگر handler قبلاً توکن را Waiting/Failed/Terminated کرده، نباید حرکت کنیم
        if (token.State is TokenState.Waiting or TokenState.Terminated or TokenState.Failed)
            return;

        var gateway = (BpmnGateway)element;
        var outgoing = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);

        if (outgoing == null || outgoing.Count == 0)
            return;

        // ✅ Split/Fork فقط اینجا
        if (outgoing.Count > 1)
        {
            var splitHandled = await _split.TrySplitAsync(process, token, gateway, ctx, ct);
            if (splitHandled)
                return;

            // اگر به هر دلیلی split-service false داد، fallback به route
            _logger.LogWarning("[GW:NAV] Split expected but not handled. Falling back to routing. Gw={Gw}", gateway.id);
        }

        // ✅ Route ساده: 1 outgoing (یا fallback)
        var chosen = outgoing.Count == 1 ? outgoing[0] : ChooseFlow(process, token, element, outgoing);

        if (chosen == null || string.IsNullOrWhiteSpace(chosen.targetRef))
        {
            _logger.LogError("[GW:NAV] No valid outgoing selected. ElementId={ElementId}", token.CurrentElementId);
            return;
        }

        await Mediator.Send(new MoveTokenCommand(
            ProcessId: process.Id,
            TokenId: token.Id,
            NextElementId: chosen.targetRef!,
            ViaFlowId: chosen.id), ct);
    }
}
