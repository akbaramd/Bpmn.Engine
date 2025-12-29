using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Commands.NodeDispatch;

public sealed record DispatchNodeNavigateCommand(Guid NodeInstanceId) : IRequest;

public sealed class DispatchNodeNavigateCommandHandler
    : IRequestHandler<DispatchNodeNavigateCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly IBpmnRuntimeContextFactory _ctxFactory;
    private readonly INodeExecutionDispatcher _dispatcher;
    private readonly ILogger<DispatchNodeNavigateCommandHandler> _logger;

    public DispatchNodeNavigateCommandHandler(
        IUnitOfWork uow,
        IBpmnRuntimeContextFactory ctxFactory,
        INodeExecutionDispatcher dispatcher,
        ILogger<DispatchNodeNavigateCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _ctxFactory = ctxFactory ?? throw new ArgumentNullException(nameof(ctxFactory));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(DispatchNodeNavigateCommand request, CancellationToken ct)
    {
        if (request.NodeInstanceId == Guid.Empty)
            throw new ArgumentException("NodeInstanceId cannot be empty.", nameof(request.NodeInstanceId));

        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            // ------------------------------------------------------------
            // 1) Load node
            // ------------------------------------------------------------
            var node = await _uow.NodeInstances.GetByIdAsync(request.NodeInstanceId, trxCt);
            if (node is null)
                throw new InvalidOperationException($"NodeInstance '{request.NodeInstanceId}' not found.");

            // Only COMPLETED nodes can navigate
            if (node.State != NodeState.Completed)
            {
                _logger.LogDebug("[NODE-NAV] Skip. NodeId={NodeId} State={State}", node.Id, node.State);
                return;
            }

            // ------------------------------------------------------------
            // 2) Load token + process + deployment
            // ------------------------------------------------------------
            var token = await _uow.Tokens.GetByIdAsync(node.TokenId, trxCt)
                       ?? throw new InvalidOperationException($"Token '{node.TokenId}' not found.");

            var process = await _uow.Processes.GetByIdAsync(node.ProcessId, trxCt)
                          ?? throw new InvalidOperationException($"Process '{node.ProcessId}' not found.");

            _ = await _uow.Deployments.GetByIdAsync(process.DeploymentId, trxCt)
                ?? throw new InvalidOperationException($"Deployment '{process.DeploymentId}' not found.");

            // ------------------------------------------------------------
            // 3) Safety: stale check (node must still match token position)
            // ------------------------------------------------------------
            if (!string.Equals(token.CurrentElementId, node.ElementId, StringComparison.Ordinal))
            {
                _logger.LogDebug(
                    "[NODE-NAV] Stale node ignored. NodeId={NodeId} NodeElement={NodeElement} TokenElement={TokenElement}",
                    node.Id, node.ElementId, token.CurrentElementId);
                return;
            }

            // ------------------------------------------------------------
            // 4) Build runtime context
            // ------------------------------------------------------------
            var ctx = await _ctxFactory.CreateAsync(process, trxCt);

            var element = ctx.Model.GetElementById(process.ProcessBpmnId, node.ElementId);
            if (element is null)
                throw new InvalidOperationException(
                    $"BPMN element '{node.ElementId}' not found in process '{process.ProcessBpmnId}'.");

            // ------------------------------------------------------------
            // 5) Dispatch NAVIGATE phase
            // ------------------------------------------------------------
            _logger.LogInformation(
                "[NODE-NAV] Dispatching. NodeId={NodeId} ElementId={ElementId} TokenId={TokenId}",
                node.Id, node.ElementId, token.Id);

            await _dispatcher.DispatchNavigateAsync(
                process: process,
                token: token,
                node: node,
                element: element,
                ctx: ctx,
                isResume: false,
                ct: trxCt);

            // If your dispatcher modifies aggregates, ensure they get persisted:
            // await _uow.SaveChangesAsync(trxCt); // only if your ExecuteInTransactionAsync does NOT auto-save.
        }, ct);
    }
}
