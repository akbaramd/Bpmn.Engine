using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;

public sealed record DispatchNodeProcessCommand(Guid NodeInstanceId, bool IsResume = false) : IRequest;

public sealed class DispatchNodeProcessCommandHandler
    : IRequestHandler<DispatchNodeProcessCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly IBpmnRuntimeContextFactory _ctxFactory;
    private readonly INodeExecutionDispatcher _dispatcher;
    private readonly ILogger<DispatchNodeProcessCommandHandler> _logger;

    public DispatchNodeProcessCommandHandler(
        IUnitOfWork uow,
        IBpmnRuntimeContextFactory ctxFactory,
        INodeExecutionDispatcher dispatcher,
        ILogger<DispatchNodeProcessCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _ctxFactory = ctxFactory ?? throw new ArgumentNullException(nameof(ctxFactory));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(DispatchNodeProcessCommand request, CancellationToken ct)
    {
        if (request.NodeInstanceId == Guid.Empty)
            throw new ArgumentException("NodeInstanceId is empty.", nameof(request.NodeInstanceId));

        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            // ------------------------------------------------------------
            // 1) Load Node (tracked)
            // ------------------------------------------------------------
            var node = await _uow.NodeInstances.GetByIdAsync(request.NodeInstanceId, trxCt);
            if (node is null)
            {
                _logger.LogDebug("[NODE-PROC] Node not found. NodeId={NodeId}", request.NodeInstanceId);
                return;
            }

            // terminal/waiting nodes should not be processed
            if (node.State is NodeState.Completed or NodeState.Failed or NodeState.Skipped or NodeState.Waiting)
            {
                _logger.LogDebug("[NODE-PROC] Skip. NodeId={NodeId} State={State}", node.Id, node.State);
                return;
            }

            // ------------------------------------------------------------
            // 2) Load Token + Process + Deployment
            // ------------------------------------------------------------
            var token = await _uow.Tokens.GetByIdAsync(node.TokenId, trxCt);
            if (token is null)
            {
                _logger.LogWarning("[NODE-PROC] Token not found. TokenId={TokenId} NodeId={NodeId}", node.TokenId, node.Id);
                return;
            }

            var process = await _uow.Processes.GetByIdAsync(node.ProcessId, trxCt);
            if (process is null)
            {
                _logger.LogWarning("[NODE-PROC] Process not found. ProcessId={ProcessId} NodeId={NodeId}", node.ProcessId, node.Id);
                return;
            }

            // Note: Deployment is loaded by BpmnRuntimeContextFactory via catalog (memory-first)

            // ------------------------------------------------------------
            // 3) Guards
            // ------------------------------------------------------------
            if (!string.Equals(token.CurrentElementId, node.ElementId, StringComparison.Ordinal))
            {
                _logger.LogDebug(
                    "[NODE-PROC] Stale node ignored. NodeId={NodeId} NodeElement={NodeElement} TokenElement={TokenElement}",
                    node.Id, node.ElementId, token.CurrentElementId);
                return;
            }


            // ------------------------------------------------------------
            // 4) Node → Processing
            // ------------------------------------------------------------
            if (node.State == NodeState.Created)
                node.Start();

            // ------------------------------------------------------------
            // 5) Resolve BPMN element
            // ------------------------------------------------------------
            var ctx = await _ctxFactory.CreateAsync(process, trxCt);

            var element = ctx.Model.GetElementById(process.ProcessBpmnId, node.ElementId);
            if (element is null)
                throw new InvalidOperationException($"BPMN element '{node.ElementId}' not found.");

            // ------------------------------------------------------------
            // 6) Dispatch PROCESS
            // ------------------------------------------------------------
            _logger.LogInformation(
                "[NODE-PROC] Dispatching PROCESS. NodeId={NodeId} ElementId={ElementId} TokenId={TokenId} Resume={Resume}",
                node.Id, node.ElementId, token.Id, request.IsResume);

            var result = await _dispatcher.DispatchNodeProcessAsync(
                process: process,
                token: token,
                node: node,
                element: element,
                ctx: ctx,
                isResume: request.IsResume,
                ct: trxCt);

            // If your ExecuteInTransactionAsync does NOT auto-save, uncomment:
            // await _uow.SaveChangesAsync(trxCt);

            _logger.LogDebug("[NODE-PROC] Node processed. NodeId={NodeId} Result={Result}", node.Id, result);

        }, ct);
    }
}
