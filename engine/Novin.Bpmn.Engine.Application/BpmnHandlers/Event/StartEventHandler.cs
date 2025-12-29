using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

public sealed class StartEventHandler : BpmnElementHandlerBase
{
    private readonly ILogger<StartEventHandler> _logger;

    public StartEventHandler(
        IFeelExpressionEvaluator feel,
        ILogger<StartEventHandler> logger,
        bool includeProcessVars = false)
        : base( feel, logger, includeProcessVars)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override bool CanHandle(BpmnFlowElement element) => element is BpmnStartEvent;

    public override Task<ElementProcessResult> ProcessAsync(
        Domain.Entities.Process process,
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

        using (_logger.BeginScope(new Dictionary<string, string?>
        {
            ["ProcessId"] = process.Id.ToString(),
            ["TokenId"] = token.Id.ToString(),
            ["NodeId"] = node.Id.ToString(),
            ["ElementId"] = token.CurrentElementId,
            ["ElementType"] = element.GetType().Name,
            ["TokenState"] = token.State.ToString(),
            ["NodeState"] = node.State.ToString(),
            ["Executable"] = token.IsExecutable.ToString(),
            ["ScopeId"] = token.ScopeId?.ToString(),
            ["ArrivedVia"] = token.ArrivedViaFlowId
        }))
        {
            _logger.LogInformation(
                "[START] ProcessAsync. TokenState={TokenState} NodeState={NodeState} Exec={Exec} Resume={Resume}",
                token.State, node.State, token.IsExecutable, isResume);

            // Terminal safety
            if (token.State is TokenState.Terminated or TokenState.Failed)
                return Task.FromResult(ElementProcessResult.NoOp);

            // Idempotency: اگر دوباره dispatch شد، Completed بده تا pipeline گیر نکند
            if (token.State != TokenState.Active)
                return Task.FromResult(ElementProcessResult.Completed);

            // Optional: مدل خراب (بدون outgoing) -> باز هم Processed می‌زنیم
            var outgoing = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);
            if (outgoing is null || outgoing.Count == 0)
                _logger.LogWarning("[START] No outgoing flow from StartEvent. Processing anyway.");

            // ✅ StartEvent: هیچ کاری ندارد، فقط "done" می‌شود
            // TokenProcessedEvent => بعداً NavigateAsync اجرا می‌شود
            token.Processed();
            node.Complete();

            return Task.FromResult(ElementProcessResult.Completed);
        }
    }

    public override Task NavigateAsync(
        Domain.Entities.Process process,
        Token token,
        NodeInstance node,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
        => base.NavigateAsync(process, token, node, element, ctx, isResume, ct);
}
