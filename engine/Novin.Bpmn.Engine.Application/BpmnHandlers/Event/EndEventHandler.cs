using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

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

        // Always allow node to complete (idempotency + crash-safe)
        if (token.State is TokenState.Completed or TokenState.Terminated)
            return Task.FromResult(TokenProcessResult.Continue);

        if (token.State == TokenState.Failed)
            return Task.FromResult(TokenProcessResult.Continue);

        if (token.State != TokenState.Active)
        {
            token.Fail($"EndEvent '{endEvent.id}' reached with invalid token state '{token.State}'.",EngineErrorKind.Technical);
            return Task.FromResult(TokenProcessResult.Continue);
        }

        if (isTerminate)
        {
            token.Terminate("Terminate EndEvent reached.");
            return Task.FromResult(TokenProcessResult.Continue);
        }

   
            token.Complete("EndEvent reached.");
       

        return Task.FromResult(TokenProcessResult.Continue);
    }

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
            ["IsTerminateEnd"] = isTerminate.ToString(),
            ["Resume"] = isResume.ToString()
        }))
        {
            _logger.LogInformation(
                "[END][NODE] Closing node. Terminate={Terminate} NodeState={NodeState}",
                isTerminate, node.State);

            if (node.State is NodeState.Completed or NodeState.Failed or NodeState.Skipped)
                return Task.FromResult(ElementProcessResult.Completed);

            node.Complete();
            return Task.FromResult(ElementProcessResult.Completed);
        }
    }

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
