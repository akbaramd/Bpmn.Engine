using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.Engine.Domain.Exceptions;

namespace Novin.Bpmn.Engine.Application.EventHandlers
{
    public sealed class NodeExecutionDispatcher : INodeExecutionDispatcher
    {
        private readonly ILogger<NodeExecutionDispatcher> _logger;
        private readonly IBpmnElementHandler[] _handlers;

        public NodeExecutionDispatcher(
            IEnumerable<IBpmnElementHandler> handlers,
            ILogger<NodeExecutionDispatcher> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _handlers = handlers?.ToArray() ?? throw new ArgumentNullException(nameof(handlers));
        }

        public async Task<ElementProcessResult> DispatchNodeProcessAsync(
            Process process,
            Token token,
            NodeInstance node,
            BpmnFlowElement element,
            BpmnRuntimeContext ctx,
            bool isResume,
            CancellationToken ct)
        {
            if (process == null) throw new ArgumentNullException(nameof(process));
            if (token == null) throw new ArgumentNullException(nameof(token));
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            // Select the appropriate handler based on element type
            var (handler, elementId, elementType) = SelectHandlerOrThrow(process, token, element, phase: "PROC", isResume);

            _logger.LogInformation(
                "[NODE:DISPATCH:PROC] ✅ Selected. ProcessId={ProcessId} TokenId={TokenId} NodeId={NodeId} ElementType={ElementType} ElementId={ElementId} Handler={Handler} IsResume={IsResume} NodeState={NodeState}",
                process.Id, token.Id, node.Id, elementType, elementId, handler.GetType().Name, isResume, node.State);

            return await handler.NodeProcessAsync(process, token, node, element, ctx, isResume, ct);
        }

        public async Task<TokenProcessResult> DispatchTokenProcessAsync(
            Process process,
            Token token,
            BpmnFlowElement element,
            BpmnRuntimeContext ctx,
            bool isResume,
            CancellationToken ct)
        {
            if (process == null) throw new ArgumentNullException(nameof(process));
            if (token == null) throw new ArgumentNullException(nameof(token));
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            // Interface doesn't pass isResume, so pick a default for "queue permission check".

            // Select the appropriate handler based on element type
            var (handler, elementId, elementType) = SelectHandlerOrThrow(process, token, element, phase: "CAN", isResume);

            var canProcess = await handler.TokenProcessAsync(process, token, element, ctx,isResume, ct);

            return await Task.FromResult(canProcess);
        }

        public async Task DispatchTokenNavigateAsync(
            Process process,
            Token token,
            BpmnFlowElement element,
            BpmnRuntimeContext ctx,
            bool isResume,
            CancellationToken ct)
        {
            if (process == null) throw new ArgumentNullException(nameof(process));
            if (token == null) throw new ArgumentNullException(nameof(token));
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            var (handler, elementId, elementType) = SelectHandlerOrThrow(process, token, element, phase: "NAV", isResume);

            _logger.LogInformation(
                "[NODE:DISPATCH:NAV] ✅ Selected. ProcessId={ProcessId} TokenId={TokenId} ElementType={ElementType} ElementId={ElementId} Handler={Handler} IsResume={IsResume}",
                process.Id, token.Id, elementType, elementId, handler.GetType().Name, isResume);

            await handler.TokenNavigateAsync(process, token, element, ctx, isResume, ct);
        }

        // Helper function to select the appropriate handler for the given element
        private (IBpmnElementHandler handler, string elementId, string elementType) SelectHandlerOrThrow(
            Process process,
            Token token,
            BpmnFlowElement element,
            string phase,
            bool isResume)
        {
            var elementType = element.GetType().Name;
            var elementId = element.id ?? "unknown";

            _logger.LogDebug(
                "[NODE:DISPATCH:{Phase}] Start. ProcessId={ProcessId} TokenId={TokenId} ElementType={ElementType} ElementId={ElementId} IsResume={IsResume} ",
                phase, process.Id, token.Id, elementType, elementId, isResume);

            IBpmnElementHandler? first = null;
            IBpmnElementHandler? second = null;

            // Low-allocation loop to find matching handlers
            for (var i = 0; i < _handlers.Length; i++)
            {
                var h = _handlers[i];
                if (!h.CanHandle(element)) continue;

                if (first is null) first = h;
                else
                {
                    second = h;
                    break;
                }
            }

            if (first is null)
            {
                var error = $"No handler found for element type '{elementType}' (ElementId={elementId})";
                _logger.LogError(
                    "[NODE:DISPATCH:{Phase}] ❌ {Error} ProcessId={ProcessId} TokenId={TokenId} ",
                    phase, error, process.Id, token.Id);

                throw new TokenExecutionException(process.Id, token.Id, elementId, error);
            }

            if (second is not null)
            {
                _logger.LogWarning(
                    "[NODE:DISPATCH:{Phase}] ⚠️ Multiple handlers found. Using first. ProcessId={ProcessId} TokenId={TokenId} ElementType={ElementType} ElementId={ElementId} First={First} Second={Second}",
                    phase, process.Id, token.Id, elementType, elementId, first.GetType().Name, second.GetType().Name);
            }

            return (first, elementId, elementType);
        }
    }
}
