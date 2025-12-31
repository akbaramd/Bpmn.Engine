using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public abstract class BpmnElementHandlerBase : IBpmnElementHandler
{
    protected readonly IFeelExpressionEvaluator Feel;
    protected readonly ILogger Logger;
    private readonly bool _includeProcessVars;

    protected BpmnElementHandlerBase(
        IFeelExpressionEvaluator feel,
        ILogger logger,
        bool includeProcessVars = false)
    {
        Feel = feel ?? throw new ArgumentNullException(nameof(feel));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _includeProcessVars = includeProcessVars;
    }

    public abstract bool CanHandle(BpmnFlowElement element);

    // -----------------------------
    // TOKEN PROCESS (default: let it continue)
    // -----------------------------
    public virtual Task<TokenProcessResult> TokenProcessAsync(
        Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
        => Task.FromResult(TokenProcessResult.Continue);

    // -----------------------------
    // NODE PROCESS (must be implemented)
    // -----------------------------
    public abstract Task<ElementProcessResult> NodeProcessAsync(
        Process process,
        Token token,
        NodeInstance node,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct);

    // -----------------------------
    // TOKEN NAVIGATION (default BPMN navigation)
    // -----------------------------
    public virtual Task TokenNavigateAsync(
        Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        // Terminal/no-move states
        if (token.State is TokenState.Waiting or TokenState.Terminated or TokenState.Failed)
            return Task.CompletedTask;

        var outgoing = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);
        if (outgoing is null || outgoing.Count == 0)
            return Task.CompletedTask;

        var selected = outgoing.Count == 1
            ? outgoing[0]
            : SelectFlow(process, token, element, outgoing);

        if (string.IsNullOrWhiteSpace(selected?.targetRef))
        {
            Logger.LogError("[NAV] No valid outgoing flow. Element={ElementId}", token.CurrentElementId);
            return Task.CompletedTask;
        }

        // If your engine spawns a child token after Processed/Completed:
        if (token.State == TokenState.Completed)
        {
            var child = new Token(
                processId: process.Id,
                startElementId: selected.targetRef!,
                parentTokenIds: new[] { token.Id });

            child.SetArrivedVia(selected.id);
            process.AddToken(child.Id);

            Logger.LogDebug("[NAV] Child token created. Parent={Parent} Child={Child} Target={Target}",
                token.Id, child.Id, selected.targetRef);

            return Task.CompletedTask;
        }

        // Otherwise move the token
        token.MoveTo(selected.targetRef!, selected.id);

        Logger.LogDebug("[NAV] Token moved. Token={TokenId} To={Target}", token.Id, selected.targetRef);
        return Task.CompletedTask;
    }

    // -----------------------------
    // FLOW SELECTION (default)
    // -----------------------------
    protected virtual BpmnSequenceFlow? SelectFlow(
        Process process,
        Token token,
        BpmnFlowElement element,
        IReadOnlyList<BpmnSequenceFlow> outgoing)
    {
        if (!token.IsExecutable)
            return outgoing[0];

        var vars = BuildEvalVars(process, token);

        foreach (var flow in outgoing)
        {
            var condition = GetConditionText(flow);
            if (string.IsNullOrWhiteSpace(condition)) continue;

            if (SafeEval(condition!, vars))
                return flow;
        }

        if (element is BpmnGateway gw)
        {
            var def = GetGatewayDefaultFlowId(gw);
            var df = outgoing.FirstOrDefault(f => f.id == def);
            if (df != null) return df;
        }

        return outgoing[0];
    }

    protected virtual IReadOnlyDictionary<string, string?> BuildEvalVars(Process process, Token token)
    {
        if (!_includeProcessVars)
            return new Dictionary<string, string?>(token.Variables);

        var dict = new Dictionary<string, string?>(process.Variables);
        foreach (var kv in token.Variables)
            dict[kv.Key] = kv.Value;

        return dict;
    }

    protected bool SafeEval(string expr, IReadOnlyDictionary<string, string?> vars)
    {
        try { return Feel.EvaluateBoolean(expr, vars); }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[NAV] FEEL eval failed. Expr={Expr}", expr);
            return false;
        }
    }

    protected static string? GetConditionText(BpmnSequenceFlow flow)
        => flow.conditionExpression?.Text is { Length: > 0 }
            ? string.Concat(flow.conditionExpression.Text).Trim()
            : null;

    protected static string? GetGatewayDefaultFlowId(BpmnGateway gateway)
        => gateway.GetType().GetProperty("default")?.GetValue(gateway) as string;
}
