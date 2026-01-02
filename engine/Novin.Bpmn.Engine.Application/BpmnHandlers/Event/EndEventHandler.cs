using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

public sealed class EndEventHandler : BpmnElementHandlerBase
{
    private readonly ILogger<EndEventHandler> _logger;

    public EndEventHandler(
        IFeelExpressionEvaluator feel,
        ILogger<EndEventHandler> logger)
        : base(feel, logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override bool CanHandle(BpmnFlowElement element) => element is BpmnEndEvent;

    /// <summary>
    /// Token-level semantics of EndEvent:
    /// - Terminate End => terminate token (and later process cancellation rules can be applied)
    /// - Normal End => complete executable token; terminate non-executable(trace) token
    /// Idempotent: if already terminal, no-op.
    /// </summary>
    public override Task<TokenProcessResult> TokenProcessAsync(
    Domain.Entities.Process process,
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

        var endEvent = (BpmnEndEvent)element;
        var isTerminate = IsTerminateEndEvent(endEvent);

        // اگر قبلاً تمام شده، این پیام احتمالاً تکراری است
        // در این حالت بهتر است NodeProcess هم نرود (چون node هم قبلاً بسته شده)
        if (token.State is TokenState.Completed or TokenState.Terminated or TokenState.Failed)
            return Task.FromResult(TokenProcessResult.NoOp);

        if (isTerminate)
        {
            token.Terminate("Terminate EndEvent reached.");
            // با اینکه terminate شد، هنوز می‌خواهیم NodeProcess اجرا شود تا NodeInstance Complete شود.
            return Task.FromResult(TokenProcessResult.Continue);
        }

        if (token.IsExecutable)
            token.Complete();
        else
            token.Terminate("Trace token ended at EndEvent.");

        // باز هم Continue برای بستن NodeInstance
        return Task.FromResult(TokenProcessResult.Continue);
    }


    /// <summary>
    /// Node-level processing should only manage node instance lifecycle,
    /// not token termination/completion.
    /// </summary>
    public override Task<ElementProcessResult> NodeProcessAsync(
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

        var endEvent = (BpmnEndEvent)element;
        var isTerminate = IsTerminateEndEvent(endEvent);

        using (_logger.BeginScope(new Dictionary<string, string?>
        {
            ["ProcessId"] = process.Id.ToString(),
            ["TokenId"] = token.Id.ToString(),
            ["NodeId"] = node.Id.ToString(),
            ["ElementId"] = token.CurrentElementId,
            ["NodeState"] = node.State.ToString(),
            ["IsTerminateEnd"] = isTerminate.ToString()
        }))
        {
            _logger.LogInformation(
                "[END][NODE] NodeProcessAsync. Terminate={Terminate} NodeState={NodeState} Resume={Resume}",
                isTerminate, node.State, isResume);

            // idempotency for node
            if (node.State is NodeState.Completed or NodeState.Failed or NodeState.Skipped)
                return Task.FromResult(ElementProcessResult.Completed);

            node.Complete();
            return Task.FromResult(ElementProcessResult.Completed);
        }
    }

    // EndEvent => no navigation
    public override Task TokenNavigateAsync(
        Domain.Entities.Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
        => Task.CompletedTask;

    private static bool IsTerminateEndEvent(BpmnEndEvent endEvent)
    {
        var items = endEvent.Items;
        if (items == null || items.Length == 0) return false;

        for (var i = 0; i < items.Length; i++)
        {
            if (items[i] is BpmnTerminateEventDefinition)
                return true;
        }

        return false;
    }
}
