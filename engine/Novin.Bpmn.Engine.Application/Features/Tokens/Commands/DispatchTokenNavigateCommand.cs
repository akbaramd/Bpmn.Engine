using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using NodeState = Novin.Bpmn.Engine.Domain.Entities.NodeState;

namespace Novin.Bpmn.Engine.Application.Commands.NodeDispatch;

public sealed record DispatchTokenNavigateCommand(Guid TokenId) : IRequest;

public sealed class DispatchNodeNavigateCommandHandler
    : IRequestHandler<DispatchTokenNavigateCommand>
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

    public async Task Handle(DispatchTokenNavigateCommand request, CancellationToken ct)
    {
        if (request.TokenId == Guid.Empty)
            throw new ArgumentException("TokenId cannot be empty.", nameof(request.TokenId));

        await _uow.BeginTransactionAsync(ct);
        try
        {
            
 
     
                var token = await _uow.Tokens.GetByIdAsync(request.TokenId, ct)
                           ?? throw new InvalidOperationException($"Token '{request.TokenId}' not found.");

                var process = await _uow.Processes.GetByIdAsync(token.ProcessId, ct)
                              ?? throw new InvalidOperationException($"Process '{token.ProcessId}' not found.");

                // ✅ Deployment is loaded by BpmnRuntimeContextFactory via catalog (memory-first)
                var ctx = await _ctxFactory.CreateAsync(process, ct);

                var element = ctx.Model.GetElementById(process.ProcessBpmnId, token.CurrentElementId);
                if (element is null)
                    throw new InvalidOperationException(
                        $"BPMN element '{token.CurrentElementId}' not found in process '{process.ProcessBpmnId}'.");

                _logger.LogInformation(
                    "[NODE-NAV] Dispatching.ElementId={ElementId} TokenId={TokenId}",
                    token.CurrentElementId, token.Id);
                var isResume = token.State == TokenState.Waiting;
                await _dispatcher.DispatchTokenNavigateAsync(
                    process: process,
                    token: token,
                    element: element,
                    ctx: ctx,
                    isResume: isResume,
                    ct: ct);

                // اگر dispatcher تغییر روی node/token/proc می‌دهد، بهتره Update هم صریح باشه
                await _uow.Tokens.UpdateAsync(token, ct);
                await _uow.Processes.UpdateAsync(process, ct);
                await _uow.CommitTransactionAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[NODE-NAV] Failed. TokenId={TokenId}",
                request.TokenId);

            // ✅ ثبت Fail در تراکنش جدا تا rollback قبلی نابودش نکند
            await MarkNodeFailedBestEffortAsync(request.TokenId, ex, ct);

        }
    }

    private async Task MarkNodeFailedBestEffortAsync(Guid nodeInstanceId, Exception ex, CancellationToken ct)
    {
        try
        {
            await _uow.ExecuteInTransactionAsync(async ct =>
            {
        

                // (اختیاری) Fail token همزمان
                var token = await _uow.Tokens.GetByIdAsync(nodeInstanceId, ct);
                if (token is not null && token.State is not TokenState.Completed and not TokenState.Terminated)
                {
                    // اگر ErrorType/Code داری، اینجا ست کن
                    token.Fail(ex.Message);
                    await _uow.Tokens.UpdateAsync(token, ct);
                }
            }, ct);
        }
        catch (Exception failEx)
        {
            // اینجا دیگه فقط log؛ چون best-effort هست
            _logger.LogError(failEx,
                "[NODE-NAV] Failed to mark Node as Failed (best-effort). NodeId={NodeId}",
                nodeInstanceId);
        }
    }
}
