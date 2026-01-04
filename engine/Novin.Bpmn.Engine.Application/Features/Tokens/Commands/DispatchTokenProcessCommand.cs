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

/// <summary>
/// Production-ready token processing dispatcher:
/// - One transaction for normal flow
/// - Strong terminal guards (NoOp for terminal states)
/// - Correct resume detection (Waiting => Resume)
/// - Classified failures:
///   - Logical => EngineErrorKind.Logical
///   - Technical => EngineErrorKind.Technical
///   - (Optional) BPMN Error => EngineErrorKind.BpmnError + errorCode
/// - Best-effort Token.Fail in a separate transaction (rollback-safe)
/// </summary>
public sealed class DispatchTokenProcessCommandHandler
    : IRequestHandler<DispatchTokenProcessCommand, TokenProcessResult>
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

        try
        {
            TokenProcessResult result = TokenProcessResult.Failed;

            // ✅ Happy-path in ONE transaction
            await _uow.ExecuteInTransactionAsync(async trxCt =>
            {
                var token = await _uow.Tokens.GetByIdAsync(request.TokenId, trxCt);
                if (token is null)
                    throw new EngineLogicalException($"Token '{request.TokenId}' not found.");

                // Terminal/NoOp guards
                if (token.State is TokenState.Completed or TokenState.Terminated)
                {
                    _logger.LogDebug("[TOKEN:PROC] NoOp: token is terminal. TokenId={TokenId} State={State}",
                        token.Id, token.State);

                    result = TokenProcessResult.NoOp;
                    return;
                }

                if (token.State is TokenState.Failed)
                {
                    _logger.LogDebug("[TOKEN:PROC] NoOp: token already failed. TokenId={TokenId}", token.Id);
                    result = TokenProcessResult.NoOp;
                    return;
                }

                var process = await _uow.Processes.GetByIdAsync(token.ProcessId, trxCt);
                if (process is null)
                    throw new EngineLogicalException($"Process '{token.ProcessId}' not found for token '{token.Id}'.");

                // Runtime context (deployment/catalog/model parse, etc.)
                var ctx = await _ctxFactory.CreateAsync(process, trxCt);

                var element = ctx.Model.GetElementById(process.ProcessBpmnId, token.CurrentElementId);
                if (element is null)
                    throw new EngineLogicalException(
                        $"BPMN element '{token.CurrentElementId}' not found in process '{process.ProcessBpmnId}'.");

                var isResume = token.State == TokenState.Waiting;

                _logger.LogInformation(
                    "[TOKEN:PROC] Dispatching. P={ProcessId} T={TokenId} ElementId={ElementId} State={State} Resume={Resume}",
                    process.Id, token.Id, token.CurrentElementId, token.State, isResume);

                // ✅ dispatcher returns TokenProcessResult
                result = await _dispatcher.DispatchTokenProcessAsync(
                    process: process,
                    token: token,
                    element: element,
                    ctx: ctx,
                    isResume: isResume,
                    ct: trxCt);

                _logger.LogInformation(
                    "[TOKEN:PROC] Result={Result}. P={ProcessId} T={TokenId} NewState={State}",
                    result, process.Id, token.Id, token.State);

                // Persist explicitly
                await _uow.Tokens.UpdateAsync(token, trxCt);
                await _uow.Processes.UpdateAsync(process, trxCt);
            }, ct);

            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("[TOKEN:PROC] Canceled. TokenId={TokenId}", request.TokenId);
            throw;
        }
        catch (EngineLogicalException lex)
        {
            _logger.LogWarning(lex, "[TOKEN:PROC] Logical failure. TokenId={TokenId}", request.TokenId);

            await FailTokenBestEffortAsync(
                tokenId: request.TokenId,
                message: lex.Message,
                kind: EngineErrorKind.Logical,
                errorCode: null,
                incidentId: null,
                ct: ct);

            return TokenProcessResult.Failed;
        }
        // OPTIONAL: if you have a BPMN error exception type in your engine, map it here.
        // catch (BpmnErrorException bex)
        // {
        //     _logger.LogInformation(bex, "[TOKEN:PROC] BPMN error. TokenId={TokenId} Code={Code}", request.TokenId, bex.ErrorCode);
        //
        //     await FailTokenBestEffortAsync(
        //         tokenId: request.TokenId,
        //         message: bex.Message,
        //         kind: EngineErrorKind.BpmnError,
        //         errorCode: bex.ErrorCode,
        //         incidentId: null,
        //         ct: ct);
        //
        //     return TokenProcessResult.Failed;
        // }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TOKEN:PROC] Technical failure. TokenId={TokenId}", request.TokenId);

            await FailTokenBestEffortAsync(
                tokenId: request.TokenId,
                message: ex.Message,
                kind: EngineErrorKind.Technical,
                errorCode: null,
                incidentId: null,
                ct: ct);

            return TokenProcessResult.Failed;
        }
    }

    /// <summary>
    /// Fail token in a separate transaction so original rollback doesn't lose it.
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
                    _logger.LogWarning("[TOKEN:PROC] BestEffortFail: token not found. TokenId={TokenId}", tokenId);
                    return;
                }

                // Guard terminals (avoid crash loops)
                if (token.State is TokenState.Completed or TokenState.Terminated or TokenState.Failed)
                {
                    _logger.LogDebug("[TOKEN:PROC] BestEffortFail: skip (terminal). TokenId={TokenId} State={State}",
                        token.Id, token.State);
                    return;
                }

                var msg = string.IsNullOrWhiteSpace(message) ? "Unhandled engine error." : message;

                token.Fail(
                    error: msg,
                    errorType: kind,
                    errorCode: errorCode,
                    incidentId: incidentId);

                await _uow.Tokens.UpdateAsync(token, trxCt);

                _logger.LogInformation(
                    "[TOKEN:PROC] Token failed (best-effort). TokenId={TokenId} Kind={Kind} ErrorCode={ErrorCode}",
                    token.Id, kind, errorCode);
            }, ct);
        }
        catch (Exception failEx)
        {
            _logger.LogError(failEx,
                "[TOKEN:PROC] BestEffortFail failed. TokenId={TokenId} Kind={Kind}",
                tokenId, kind);
        }
    }

    /// <summary>
    /// Expected/validation/precondition failures => Logical.
    /// </summary>
    private sealed class EngineLogicalException : Exception
    {
        public EngineLogicalException(string message) : base(message) { }
    }
}
