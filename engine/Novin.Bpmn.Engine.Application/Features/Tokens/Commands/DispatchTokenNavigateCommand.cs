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

public sealed record DispatchTokenNavigateCommand(Guid TokenId) : IRequest;

/// <summary>
/// Production-ready token navigation dispatcher:
/// - Single transaction for normal flow
/// - Clear terminal guards (no retry storms)
/// - Precise error handling:
///   - Expected/Logical issues => fail token as Logical
///   - Infrastructure/Unexpected => fail token as Technical
/// - Best-effort fail in a separate transaction (so rollback doesn't lose failure state)
/// </summary>
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

        try
        {
            // ✅ Run the "happy path" inside ONE transaction
            await _uow.ExecuteInTransactionAsync(async trxCt =>
            {
                var token = await _uow.Tokens.GetByIdAsync(request.TokenId, trxCt);
                if (token is null)
                    throw new EngineLogicalException($"Token '{request.TokenId}' not found.");

                // Terminal guard: do nothing
                if (token.State is TokenState.Completed or TokenState.Terminated)
                {
                    _logger.LogDebug("[NODE-NAV] NoOp: token is terminal. TokenId={TokenId} State={State}",
                        token.Id, token.State);
                    return;
                }

                if (token.State is TokenState.Failed)
                {
                    _logger.LogDebug("[NODE-NAV] NoOp: token already failed. TokenId={TokenId}", token.Id);
                    return;
                }

                var process = await _uow.Processes.GetByIdAsync(token.ProcessId, trxCt);
                if (process is null)
                    throw new EngineLogicalException($"Process '{token.ProcessId}' not found for token '{token.Id}'.");

                // Runtime context: might throw (deployment missing, parse error, etc.)
                var ctx = await _ctxFactory.CreateAsync(process, trxCt);

                var element = ctx.Model.GetElementById(process.ProcessBpmnId, token.CurrentElementId);
                if (element is null)
                    throw new EngineLogicalException(
                        $"BPMN element '{token.CurrentElementId}' not found in process '{process.ProcessBpmnId}'.");

                var isResume = token.State == TokenState.Waiting;

                _logger.LogInformation(
                    "[NODE-NAV] Dispatching. P={ProcessId} T={TokenId} ElementId={ElementId} Resume={Resume} State={State}",
                    process.Id, token.Id, token.CurrentElementId, isResume, token.State);

                // Dispatch navigation (may mutate token/process)
                await _dispatcher.DispatchTokenNavigateAsync(
                    process: process,
                    token: token,
                    element: element,
                    ctx: ctx,
                    isResume: isResume,
                    ct: trxCt);

                // Persist (explicit updates are ok even if change-tracked)
                await _uow.Tokens.UpdateAsync(token, trxCt);
                await _uow.Processes.UpdateAsync(process, trxCt);
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Upstream cancellation => do not mark failed
            _logger.LogWarning("[NODE-NAV] Canceled. TokenId={TokenId}", request.TokenId);
            throw;
        }
        catch (EngineLogicalException lex)
        {
            _logger.LogWarning(lex, "[NODE-NAV] Logical failure. TokenId={TokenId}", request.TokenId);

            // ✅ Best-effort fail outside original transaction
            await FailTokenBestEffortAsync(
                tokenId: request.TokenId,
                message: lex.Message,
                kind: EngineErrorKind.Logical,
                errorCode: null,
                incidentId: null,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NODE-NAV] Technical failure. TokenId={TokenId}", request.TokenId);

            // ✅ Best-effort fail outside original transaction
            await FailTokenBestEffortAsync(
                tokenId: request.TokenId,
                message: ex.Message,
                kind: EngineErrorKind.Technical,
                errorCode: null,
                incidentId: null,
                ct: ct);
        }
    }

    /// <summary>
    /// Fail the token in a separate transaction so the original rollback doesn't lose it.
    /// Never throws (best-effort).
    /// </summary>
    private async Task FailTokenBestEffortAsync(
        Guid tokenId,
        string message,
        EngineErrorKind kind,
        string? errorCode,
        Guid? incidentId,
        CancellationToken ct)
    {
        try
        {
            await _uow.ExecuteInTransactionAsync(async trxCt =>
            {
                var token = await _uow.Tokens.GetByIdAsync(tokenId, trxCt);
                if (token is null)
                {
                    _logger.LogWarning("[NODE-NAV] BestEffortFail: token not found. TokenId={TokenId}", tokenId);
                    return;
                }

                // Guard terminals (avoid crash loops)
                if (token.State is TokenState.Completed or TokenState.Terminated or TokenState.Failed)
                {
                    _logger.LogDebug("[NODE-NAV] BestEffortFail: skip (terminal). TokenId={TokenId} State={State}",
                        token.Id, token.State);
                    return;
                }

                // Ensure non-empty message for Token.Fail guard
                var msg = string.IsNullOrWhiteSpace(message) ? "Unhandled engine error." : message;

                token.Fail(
                    error: msg,
                    errorType: kind,
                    errorCode: errorCode,
                    incidentId: incidentId);

                await _uow.Tokens.UpdateAsync(token, trxCt);

                _logger.LogInformation(
                    "[NODE-NAV] Token failed (best-effort). TokenId={TokenId} Kind={Kind} ErrorCode={ErrorCode}",
                    token.Id, kind, errorCode);
            }, ct);
        }
        catch (Exception failEx)
        {
            _logger.LogError(failEx,
                "[NODE-NAV] BestEffortFail failed. TokenId={TokenId} Kind={Kind}",
                tokenId, kind);
        }
    }

    /// <summary>
    /// Use this for "expected" failures that should be classified as Logical (validation/precondition).
    /// </summary>
    private sealed class EngineLogicalException : Exception
    {
        public EngineLogicalException(string message) : base(message) { }
    }
}
