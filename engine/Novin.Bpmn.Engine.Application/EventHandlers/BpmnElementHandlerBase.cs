using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.CreateToken;
using Novin.Bpmn.Engine.Application.Commands.MoveToken;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public abstract class BpmnElementHandlerBase : IBpmnElementHandler
{
    protected readonly IMediator Mediator;
    protected readonly IFeelExpressionEvaluator Feel;
    protected readonly ILogger Logger;

    // اگر خواستی condition ها process vars را هم ببینند true کن
    private readonly bool _includeProcessVars;

    protected BpmnElementHandlerBase(
        IMediator mediator,
        IFeelExpressionEvaluator feel,
        ILogger logger,
        bool includeProcessVars = false)
    {
        Mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        Feel = feel ?? throw new ArgumentNullException(nameof(feel));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _includeProcessVars = includeProcessVars;
    }

    public abstract bool CanHandle(BpmnFlowElement element);

    public abstract Task<ElementProcessResult> ProcessAsync(
        Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx, bool isResume, CancellationToken ct);

    public virtual async Task NavigateAsync(
        Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        // اگر element در ProcessAsync رفت توی Waiting/Terminate/Fail، اینجا نباید حرکت کنیم
        if (token.State is TokenState.Waiting or TokenState.Terminated or TokenState.Failed)
            return;

        var outgoing = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);
        if (outgoing is null || outgoing.Count == 0)
            return;

        // If token is completed, we need to create new tokens instead of moving the completed one
        if (token.State == TokenState.Completed)
        {
            // For completed tokens, create new tokens at target elements
            var chosen = outgoing.Count == 1
                ? outgoing[0]
                : ChooseFlow(process, token, element, outgoing);

            if (chosen == null || string.IsNullOrWhiteSpace(chosen.targetRef))
            {
                Logger.LogError("[NAV] No valid outgoing selected for completed token. ElementId={ElementId}", token.CurrentElementId);
                return;
            }

            // Create a new token at the target element
            await Mediator.Send(new Commands.CreateToken.CreateTokenCommand(
                ProcessId: process.Id,
                StartElementId: chosen.targetRef!,
                ParentTokenIds: new[] { token.Id },
                ArrivedViaFlowId: chosen.id), ct);

            Logger.LogDebug("[NAV] Created new token for completed token navigation. ProcessId={ProcessId} ParentTokenId={ParentTokenId} TargetElementId={TargetElementId}",
                process.Id, token.Id, chosen.targetRef);
            return;
        }

        // For active tokens, move them normally
        var chosenFlow = outgoing.Count == 1
            ? outgoing[0]
            : ChooseFlow(process, token, element, outgoing);

        if (chosenFlow == null || string.IsNullOrWhiteSpace(chosenFlow.targetRef))
        {
            Logger.LogError("[NAV] No valid outgoing selected. ElementId={ElementId}", token.CurrentElementId);
            return;
        }

        await Mediator.Send(new MoveTokenCommand(
            ProcessId: process.Id,
            TokenId: token.Id,
            NextElementId: chosenFlow.targetRef!,
            ViaFlowId: chosenFlow.id), ct);
    }

    protected virtual BpmnSequenceFlow? ChooseFlow(
        Process process,
        Token token,
        BpmnFlowElement element,
        IReadOnlyList<BpmnSequenceFlow> outgoing)
    {
        // Trace token => شرط‌ها را چک نکن (فقط otherwise/unconditional یا first)
        if (!token.IsExecutable)
        {
            var uncond = outgoing.FirstOrDefault(f => string.IsNullOrWhiteSpace(GetConditionText(f)));
            return uncond ?? outgoing[0];
        }

        var vars = BuildEvalVars(process, token);

        // اول شرط‌دارها به ترتیب مدل: اولین true
        foreach (var f in outgoing)
        {
            var expr = GetConditionText(f);
            if (string.IsNullOrWhiteSpace(expr)) continue;

            if (SafeEval(expr!, vars))
                return f;
        }

        // default اگر gateway بود
        if (element is BpmnGateway gw)
        {
            var defId = GetGatewayDefaultFlowId(gw);
            var df = ResolveDefaultFlow(outgoing, defId);
            if (df != null) return df;
        }

        // otherwise/unconditional
        var otherwise = outgoing.FirstOrDefault(f => string.IsNullOrWhiteSpace(GetConditionText(f)));
        if (otherwise != null) return otherwise;

        // fallback
        return outgoing[0];
    }

    protected virtual bool SafeEval(string expr, IReadOnlyDictionary<string, string?> vars)
    {
        try { return Feel.EvaluateBoolean(expr, vars); }
        catch
        {
            Logger.LogError("[NAV] Condition eval failed. Expr={Expr}", expr);
            return false;
        }
    }

    protected virtual IReadOnlyDictionary<string, string?> BuildEvalVars(Process p, Token t)
    {
        if (!_includeProcessVars)
            return new Dictionary<string, string?>(t.Variables, StringComparer.Ordinal);

        var dict = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var kv in p.Variables) dict[kv.Key] = kv.Value;
        foreach (var kv in t.Variables) dict[kv.Key] = kv.Value;
        return dict;
    }

    protected static string? GetConditionText(BpmnSequenceFlow f)
    {
        var ce = f.conditionExpression;
        if (ce?.Text == null || ce.Text.Length == 0) return null;
        var raw = string.Concat(ce.Text).Trim();
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    protected static string? GetGatewayDefaultFlowId(BpmnGateway gateway)
    {
        var gt = gateway.GetType();
        return gt.GetProperty("default")?.GetValue(gateway) as string
               ?? gt.GetProperty("Default")?.GetValue(gateway) as string;
    }

    protected static BpmnSequenceFlow? ResolveDefaultFlow(IReadOnlyList<BpmnSequenceFlow> outgoing, string? defaultFlowId)
    {
        if (string.IsNullOrWhiteSpace(defaultFlowId)) return null;

        return outgoing.FirstOrDefault(f =>
            string.Equals(f.id, defaultFlowId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(FlowKey(f), defaultFlowId, StringComparison.OrdinalIgnoreCase));
    }

    protected static string FlowKey(BpmnSequenceFlow f)
        => !string.IsNullOrWhiteSpace(f.id) ? f.id! : $"{f.sourceRef}->{f.targetRef}";
}
