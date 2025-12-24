using System.Reflection;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Services.Feel;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

public interface IVariableMappingService
{
    void ApplyInputs(Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx);
    void ApplyOutputs(Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx);
}

public sealed class BonyanVariableMappingService : IVariableMappingService
{
    private readonly IFeelExpressionEvaluator _feel;
    private readonly ILogger<BonyanVariableMappingService> _logger;

    public BonyanVariableMappingService(IFeelExpressionEvaluator feel, ILogger<BonyanVariableMappingService> logger)
    {
        _feel = feel ?? throw new ArgumentNullException(nameof(feel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void ApplyInputs(Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx)
    {
        if (token.State is TokenState.Completed or TokenState.Terminated) return;

        var map = GetIoMapping(element);
        if (map == null || map.Input.Count == 0)
        {
            _logger.LogDebug("[MAP:IN] none. Element={ElementId}", element.id);
            return;
        }

        foreach (var input in map.Input)
        {
            var src = input.Source?.Trim();
            var tgt = input.Target?.Trim();

            if (string.IsNullOrWhiteSpace(tgt))
                continue;

            if (string.IsNullOrWhiteSpace(src))
            {
                HandleMissingInput(map.OnMissingSource, token, tgt, "empty source");
                continue;
            }

            object? value;
            var isFeel = src.StartsWith("=");

            if (!isFeel)
            {
                // plain process var
                if (!process.Variables.TryGetValue(src, out value))
                {
                    HandleMissingInput(map.OnMissingSource, token, tgt, $"missing process var '{src}'");
                    continue;
                }
            }
            else
            {
                // FEEL expression based on process vars
                var expr = src.Substring(1).Trim();
                try
                {
                    value = _feel.Evaluate(expr, process.Variables); // این Evaluate باید object? بده
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[MAP:IN] FEEL eval failed. Expr={Expr} Element={ElementId}", expr, element.id);
                    HandleMissingInput(map.OnMissingSource, token, tgt, "feel eval failed");
                    continue;
                }
            }

            token.SetVariable(tgt, value);
            _logger.LogDebug("[MAP:IN] {Src} -> {Tgt} = {Val}", src, tgt, value);
        }
    }

    public void ApplyOutputs(Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx)
    {
        if (token.State is TokenState.Completed or TokenState.Terminated) return;

        var map = GetIoMapping(element);
        if (map == null || map.Output.Count == 0)
        {
            _logger.LogDebug("[MAP:OUT] none. Element={ElementId}", element.id);
            return;
        }

        foreach (var output in map.Output)
        {
            var src = output.Source?.Trim();
            var tgt = output.Target?.Trim();

            if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(tgt))
                continue;

            if (!token.TryGetVariable(src, out var val))
            {
                HandleMissingOutput(map.OnMissingOutput, process, token, tgt, $"missing token var '{src}'");
                continue;
            }

            // overwrite policy
            if (!map.Overwrite && process.Variables.ContainsKey(tgt))
            {
                _logger.LogDebug("[MAP:OUT] skip overwrite. Target={Tgt}", tgt);
                continue;
            }

            // ✅ درست: تغییر از طریق متد دامنه
            process.SetVariable(tgt, val);
            _logger.LogDebug("[MAP:OUT] {Src} -> {Tgt} = {Val}", src, tgt, val);
        }
    }

    private static BonyanIoMapping? GetIoMapping(BpmnFlowElement element)
    {
        // اگر نود از نوع BpmnFlowNode است (اکثراً همین است)
        if (element is BpmnFlowNode node && node.BonyanIoMapping is not null)
            return node.BonyanIoMapping;

        // در صورتی که از کلاس‌های مشتق‌شده باشد (مثل BpmnScriptTask, Gateway, ...)
        var prop = element.GetType().GetProperty(nameof(BpmnFlowNode.BonyanIoMapping),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

        if (prop?.GetValue(element) is BonyanIoMapping io)
            return io;

        // اگر هیچ موردی پیدا نشد
        return null;
    }

    private void HandleMissingInput(MissingBehavior policy, Token token, string target, string reason)
    {
        switch (policy)
        {
            case MissingBehavior.Skip:
                _logger.LogDebug("[MAP:IN] missing -> skip. Target={Target} Reason={Reason}", target, reason);
                break;

            case MissingBehavior.Null:
                token.SetVariable(target, null);
                _logger.LogDebug("[MAP:IN] missing -> null. Target={Target} Reason={Reason}", target, reason);
                break;

            case MissingBehavior.Fail:
                token.Fail($"IO input missing for '{target}': {reason}");
                _logger.LogWarning("[MAP:IN] missing -> FAIL. Target={Target} Reason={Reason}", target, reason);
                break;
        }
    }

    private void HandleMissingOutput(MissingBehavior policy, Process process, Token token, string target, string reason)
    {
        switch (policy)
        {
            case MissingBehavior.Skip:
                _logger.LogDebug("[MAP:OUT] missing -> skip. Target={Target} Reason={Reason}", target, reason);
                break;

            case MissingBehavior.Null:
                process.SetVariable(target, null);
                _logger.LogDebug("[MAP:OUT] missing -> null. Target={Target} Reason={Reason}", target, reason);
                break;

            case MissingBehavior.Fail:
                token.Fail($"IO output missing for '{target}': {reason}");
                _logger.LogWarning("[MAP:OUT] missing -> FAIL. Target={Target} Reason={Reason}", target, reason);
                break;
        }
    }
}
