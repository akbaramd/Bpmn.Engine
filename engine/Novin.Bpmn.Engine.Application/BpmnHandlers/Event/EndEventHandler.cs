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

    public override async Task<ElementProcessResult> NodeProcessAsync(
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
            ["Executable"] = token.IsExecutable.ToString(),
            ["TokenState"] = token.State.ToString(),
            ["NodeState"] = node.State.ToString(),
            ["IsTerminateEnd"] = isTerminate.ToString()
        }))
        {
            _logger.LogInformation(
                "[END] NodeProcessAsync. Terminate={Terminate} TokenState={TokenState} NodeState={NodeState} Exec={Exec} Resume={Resume}",
                isTerminate, token.State, node.State, token.IsExecutable, isResume);

            // Terminal safety + idempotency: اگر دوباره رسیدیم، Completed بده
            if (token.State is TokenState.Terminated or TokenState.Completed or TokenState.Failed)
                return ElementProcessResult.Completed;

            if (isTerminate)
            {
                token.Terminate();

                return ElementProcessResult.Completed;
            }

            node.Complete();
            // Normal End: only end this token
            if (token.IsExecutable)
            {
                
                token.Complete();
            }
            else
            {
                token.Terminate("Trace token ended at EndEvent.");
            }

            // Optional: if NodeInstance tracks lifecycle separately, close it too.
            // If you have node.Complete()/node.Terminate(), call them here.
            // Otherwise leave it to your NodeEvent pipeline.

            return ElementProcessResult.Completed;
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
