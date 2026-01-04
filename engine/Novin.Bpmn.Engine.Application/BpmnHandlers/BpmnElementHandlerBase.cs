using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
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

    // برای لاگ امن: چند کلید اول
    private const int MaxVarKeysToLog = 20;

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

    public virtual Task<TokenProcessResult> TokenProcessAsync(
        Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
        => Task.FromResult(TokenProcessResult.Continue);

    public abstract Task<ElementProcessResult> NodeProcessAsync(
        Process process,
        Token token,
        NodeInstance node,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct);

    public virtual Task TokenNavigateAsync(
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

        // اگر بعد از NodeProcess، توکن Completed شده و شما child-token می‌سازید:
        if (token.State == TokenState.Completed)
        {
            var child = new Token(
                processId: process.Id,
                startElementId: selected.targetRef!,
                parentTokenId: token.Id);

            child.SetArrivedVia(selected.id);
            process.AddToken(child.Id);

            Logger.LogDebug("[NAV] Child token created. Parent={Parent} Child={Child} Target={Target}",
                token.Id, child.Id, selected.targetRef);

            return Task.CompletedTask;
        }

        // حرکت توکن
        token.MoveTo(selected.targetRef!, false, selected.id);

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
        var vars = BuildEvalVars(process, token);

        Logger.LogDebug(
            "[NAV] Selecting flow. Outgoing={Count} VarsCount={VarsCount} VarsKeys={VarsKeys}",
            outgoing.Count,
            vars.Count,
            string.Join(",", vars.Keys.Take(MaxVarKeysToLog)));

        foreach (var flow in outgoing)
        {
            var condition = GetConditionText(flow);
            if (string.IsNullOrWhiteSpace(condition))
                continue; // شرط ندارد => برای fallback

            if (SafeEval(condition!, vars))
                return flow;
        }

        // default flow برای gateway
        if (element is BpmnGateway gw)
        {
            var def = GetGatewayDefaultFlowId(gw);
            if (!string.IsNullOrWhiteSpace(def))
            {
                var df = outgoing.FirstOrDefault(f => f.id == def);
                if (df != null) return df;
            }
        }

        // fallback: اولی
        return outgoing[0];
    }

    // -----------------------------
    // Vars for FEEL
    // -----------------------------
    protected virtual IReadOnlyDictionary<string, JsonNode?> BuildEvalVars(Process p, Token t)
    {
        if (!_includeProcessVars)
        {
            // فقط Token vars
            return new Dictionary<string, JsonNode?>(t.Variables, StringComparer.Ordinal);
        }

        // token overrides process
        var dict = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);

        foreach (var kv in p.VariablesObject)
            dict[kv.Key] = kv.Value;

        foreach (var kv in t.Variables)
            dict[kv.Key] = kv.Value;

        return dict;
    }

    protected bool SafeEval(string expr, IReadOnlyDictionary<string, JsonNode?> vars)
    {
        try
        {
            // expr می‌تواند "= flag == true" باشد؛ Normalize در FeelExpressionEvaluator انجام می‌شود
            return Feel.EvaluateBoolean(expr, vars);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "[NAV] FEEL eval failed. Expr={Expr} VarsCount={VarsCount} VarsKeys={VarsKeys}",
                expr,
                vars.Count,
                string.Join(",", vars.Keys.Take(MaxVarKeysToLog)));

            return false;
        }
    }

    // -----------------------------
    // Condition extraction
    // -----------------------------
    protected static string? GetConditionText(BpmnSequenceFlow flow)
        => flow.conditionExpression?.Text is { Length: > 0 }
            ? string.Concat(flow.conditionExpression.Text).Trim()
            : null;

    protected static string? GetGatewayDefaultFlowId(BpmnGateway gateway)
        => gateway.GetType().GetProperty("default")?.GetValue(gateway) as string
           ?? gateway.GetType().GetProperty("Default")?.GetValue(gateway) as string;
}
