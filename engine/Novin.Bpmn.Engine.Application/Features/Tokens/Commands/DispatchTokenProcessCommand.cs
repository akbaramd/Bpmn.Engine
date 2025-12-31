using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Commands.NodeDispatch;

public sealed record DispatchTokenProcessCommand(Guid TokenId) : IRequest<TokenProcessResult>;

public sealed class DispatchTokenProcessCommandHandler : IRequestHandler<DispatchTokenProcessCommand,TokenProcessResult>
{
    private readonly IUnitOfWork _uow;
    private readonly IBpmnRuntimeContextFactory _ctxFactory;
    private readonly INodeExecutionDispatcher _dispatcher;
    private readonly ILogger<DispatchTokenProcessCommandHandler> _logger;

    public DispatchTokenProcessCommandHandler(
        IUnitOfWork uow,
        IBpmnRuntimeContextFactory ctxFactory,
        INodeExecutionDispatcher dispatcher,
        ILogger<DispatchTokenProcessCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _ctxFactory = ctxFactory ?? throw new ArgumentNullException(nameof(ctxFactory));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TokenProcessResult> Handle(DispatchTokenProcessCommand request, CancellationToken ct)
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

            _ = await _uow.Deployments.GetByIdAsync(process.DeploymentId, ct)
                ?? throw new InvalidOperationException($"Deployment '{process.DeploymentId}' not found.");

            var ctx = await _ctxFactory.CreateAsync(process, ct);

            var element = ctx.Model.GetElementById(process.ProcessBpmnId, token.CurrentElementId);
            if (element is null)
                throw new InvalidOperationException(
                    $"BPMN element '{token.CurrentElementId}' not found in process '{process.ProcessBpmnId}'.");

            // پیشنهاد: resume وقتی true شود که token قبلاً Waiting بوده و الان trigger شده
            var isResume = token.State == TokenState.Waiting;

            _logger.LogInformation(
                "[TOKEN:PROC] Dispatching. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId} State={State} IsResume={IsResume}",
                process.Id, token.Id, token.CurrentElementId, token.State, isResume);

            // ✅ این باید TokenProcessResult برگرداند (نه bool)
            var tokenResult = await _dispatcher.DispatchTokenProcessAsync(
                process: process,
                token: token,
                element: element,
                ctx: ctx,
                isResume: isResume,
                ct: ct);

            _logger.LogInformation(
                "[TOKEN:PROC] Result={Result}. ProcessId={ProcessId} TokenId={TokenId} State={State}",
                tokenResult, process.Id, token.Id, token.State);

            // اگر تغییرات داخل handler/dispatcher روی توکن/پروسس انجام شده
            await _uow.Tokens.UpdateAsync(token, ct);
            await _uow.Processes.UpdateAsync(process, ct);


            return tokenResult;
            await _uow.CommitTransactionAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return TokenProcessResult.Failed;
            // cancellation => rollback & rethrow
            await SafeRollbackAsync(ct);
            throw;
        }
        catch (Exception ex)
        {
            return TokenProcessResult.Failed;
            await SafeRollbackAsync(ct);

            _logger.LogError(ex, "[TOKEN:PROC] Failed. TokenId={TokenId}", request.TokenId);

            // ✅ best-effort fail token در تراکنش جدا
            await MarkTokenFailedBestEffortAsync(request.TokenId, ex, ct);
        }

       
    }

    private async Task SafeRollbackAsync(CancellationToken ct)
    {
        try
        {
            await _uow.RollbackTransactionAsync(ct);
        }
        catch (Exception rbEx)
        {
            _logger.LogError(rbEx, "[TOKEN:PROC] Rollback failed.");
        }
    }

    private async Task MarkTokenFailedBestEffortAsync(Guid tokenId, Exception ex, CancellationToken ct)
    {
        try
        {
            await _uow.ExecuteInTransactionAsync(async ict =>
            {
                var token = await _uow.Tokens.GetByIdAsync(tokenId, ict);
                if (token is null) return;

                if (token.State is not TokenState.Completed and not TokenState.Terminated and not TokenState.Failed)
                {
                    token.Fail(ex.Message);
                    await _uow.Tokens.UpdateAsync(token, ict);
                }
            }, ct);
        }
        catch (Exception failEx)
        {
            _logger.LogError(failEx, "[TOKEN:PROC] Failed to mark Token as Failed (best-effort). TokenId={TokenId}", tokenId);
        }
    }
}
