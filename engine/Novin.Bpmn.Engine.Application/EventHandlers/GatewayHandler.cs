using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

public sealed class GatewayHandler : IBpmnElementHandler
{
    private readonly IGatewayJoinService _join;
    private readonly IGatewaySplitService _split;
    private readonly ITokenNavigationService _nav;
    private readonly ILogger<GatewayHandler> _logger;

    public GatewayHandler(
        IGatewayJoinService join,
        IGatewaySplitService split,
        ITokenNavigationService nav,
        ILogger<GatewayHandler> logger)
    {
        _join = join ?? throw new ArgumentNullException(nameof(join));
        _split = split ?? throw new ArgumentNullException(nameof(split));
        _nav = nav ?? throw new ArgumentNullException(nameof(nav));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool CanHandle(BpmnFlowElement element) => element is BpmnGateway;

    public async Task HandleAsync(Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx, CancellationToken ct)
    {
        var gateway = (BpmnGateway)element;

        var incoming = ctx.Model.GetIncomingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);
        var outgoing = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProcessId"] = process.Id,
            ["TokenId"] = token.Id,
            ["GatewayId"] = gateway.id,
            ["GatewayType"] = gateway.GetType().Name,
            ["ElementId"] = token.CurrentElementId,
            ["TokenState"] = token.State.ToString(),
            ["ScopeId"] = token.ScopeId,
            ["ArrivedVia"] = token.ArrivedViaFlowId,
            ["Executable"] = token.IsExecutable,
            ["IncomingCount"] = incoming.Count,
            ["OutgoingCount"] = outgoing.Count
        }))
        {
            _logger.LogInformation("[GW] Enter gateway. State={State} Exec={Exec} Incoming={InCnt} Outgoing={OutCnt}",
                token.State, token.IsExecutable, incoming.Count, outgoing.Count);

            // 1) Join (اگر join باشد باید قبل از split اجرا شود)
            var joined = await _join.TryJoinAsync(process, token, gateway, ctx, ct);
            _logger.LogInformation("[GW] JoinAttempt={Joined} StateAfter={State}", joined, token.State);

            if (joined)
                return;

            // 2) Split
            var split = await _split.TrySplitAsync(process, token, gateway, ctx, ct);
            _logger.LogInformation("[GW] SplitAttempt={Split} StateAfter={State}", split, token.State);

            if (split)
                return;

            // 3) Normal navigation (should be rare for gateways)
            _logger.LogInformation("[GW] No join/split -> navigation.");
            await _nav.MoveNextOrForkAsync(process, token, ctx, executableMode: token.IsExecutable, ct);
        }
    }
}
