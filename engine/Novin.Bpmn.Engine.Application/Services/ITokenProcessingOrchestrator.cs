using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.Exceptions;
using Novin.Bpmn.Engine.Domain.ValueObjects;

/// <summary>
/// Determines if a token is "active" (should be converted to trace token).
/// Active tokens are in Created/Active/Waiting states.
/// </summary>
internal static class TokenExtensions
{
    internal static bool IsActiveToken(Token token)
    {
        return token.State is TokenState.Created
            or TokenState.Active
            or TokenState.Waiting;
    }
}

public interface ITokenProcessingOrchestrator
{
    Task ProcessAsync(Guid processId, Guid tokenId, CancellationToken ct);
}

public sealed class TokenProcessingOrchestrator : ITokenProcessingOrchestrator
{
    private readonly IUnitOfWork _uow;
    private readonly IBpmnRuntimeContextFactory _ctxFactory;
    private readonly ITokenExecutionDispatcher _dispatcher;
    private readonly IIncidentService _incidentService;
    private readonly IBpmnErrorBoundaryFinder _errorBoundaryFinder;
    private readonly IBoundaryEventExecutor _boundaryEventExecutor;
    private readonly IBoundaryTimerScheduler _timerScheduler;
    private readonly IMediator _mediator;
    private readonly ILogger<TokenProcessingOrchestrator> _logger;

    public TokenProcessingOrchestrator(
        IUnitOfWork uow,
        IBpmnRuntimeContextFactory ctxFactory,
        ITokenExecutionDispatcher dispatcher,
        IIncidentService incidentService,
        IBpmnErrorBoundaryFinder errorBoundaryFinder,
        IBoundaryEventExecutor boundaryEventExecutor,
        IBoundaryTimerScheduler timerScheduler,
        IMediator mediator,
        ILogger<TokenProcessingOrchestrator> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _ctxFactory = ctxFactory ?? throw new ArgumentNullException(nameof(ctxFactory));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _incidentService = incidentService ?? throw new ArgumentNullException(nameof(incidentService));
        _errorBoundaryFinder = errorBoundaryFinder ?? throw new ArgumentNullException(nameof(errorBoundaryFinder));
        _boundaryEventExecutor = boundaryEventExecutor ?? throw new ArgumentNullException(nameof(boundaryEventExecutor));
        _timerScheduler = timerScheduler ?? throw new ArgumentNullException(nameof(timerScheduler));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ProcessAsync(Guid processId, Guid tokenId, CancellationToken ct)
    {
        // ─────────────────────────────────────────────────────────
        // Tx1: اجرای نود (اگر exception رخ دهد، rollback می‌شود)
        // ─────────────────────────────────────────────────────────
        try
        {
            await _uow.ExecuteInTransactionAsync(async trxCt =>
            {
                var process = await _uow.Processes.GetByIdAsync(processId, trxCt);
                if (process == null)
                {
                    _logger.LogWarning("[ORCHESTRATOR] Process not found. ProcessId={ProcessId}", processId);
                    return;
                }

                var token = await _uow.Tokens.GetByIdAsync(tokenId, trxCt);
                if (token == null)
                {
                    _logger.LogWarning("[ORCHESTRATOR] Token not found. TokenId={TokenId}", tokenId);
                    return;
                }

                if (token.State != TokenState.Active)
                {
                    _logger.LogDebug(
                        "[ORCHESTRATOR] Token not in Active state. TokenId={TokenId} State={State}",
                        tokenId,
                        token.State);
                    return;
                }

                var ctx = await _ctxFactory.CreateAsync(process, trxCt);

                var element = ctx.Model.GetElementById(ctx.BpmnProcessId, token.CurrentElementId);
                if (element == null)
                {
                    // این یک technical failure است (element پیدا نشد)
                    throw new TokenExecutionException(
                        processId,
                        tokenId,
                        token.CurrentElementId,
                        $"Element '{token.CurrentElementId}' not found.");
                }

                using (_logger.BeginScope(new Dictionary<string, object?>
                {
                    ["ProcessId"] = process.Id,
                    ["TokenId"] = token.Id,
                    ["ElementId"] = token.CurrentElementId,
                    ["ScopeId"] = token.ScopeId,
                    ["ArrivedVia"] = token.ArrivedViaFlowId,
                    ["Executable"] = token.IsExecutable,
                    ["State"] = token.State.ToString()
                }))
                {
                    await _dispatcher.DispatchAsync(process, token, element, ctx, trxCt);
                }
            }, ct);
        }
        catch (BpmnErrorException bex)
        {
            _logger.LogWarning(
                "[ORCHESTRATOR] ⚠️ BPMN Error caught in ProcessTokenAsync. ProcessId={ProcessId} TokenId={TokenId} ErrorCode={ErrorCode} Message={Message}",
                processId,
                tokenId,
                bex.Code,
                bex.Message);
            
            // BPMN Error: باید در یک transaction جدا handle شود
            await HandleBpmnErrorAsync(processId, tokenId, bex, ct);
        }
        catch (TokenExecutionException tex)
        {
            // Technical Failure: باید در یک transaction جدا handle شود
            await HandleTechnicalFailureAsync(processId, tokenId, tex, ct);
        }
        catch (Exception ex)
        {
            // هر exception دیگری هم technical failure است
            await HandleTechnicalFailureAsync(
                processId,
                tokenId,
                new TokenExecutionException(processId, tokenId, "unknown", ex),
                ct);
        }
    }

    /// <summary>
    /// Handle BPMN Error according to BPMN 2.0 semantics:
    /// 1. Try to find Error Boundary or Error EventSubprocess
    /// 2. If found: propagate token to error handler
    /// 3. If not found: terminate token (unhandled BPMN error - not an incident)
    /// </summary>
    private async Task HandleBpmnErrorAsync(
        Guid processId,
        Guid tokenId,
        BpmnErrorException bex,
        CancellationToken ct)
    {
        _logger.LogWarning(
            "[ORCHESTRATOR] BPMN Error occurred. ProcessId={ProcessId} TokenId={TokenId} ErrorCode={ErrorCode} Message={Message}",
            processId,
            tokenId,
            bex.Code,
            bex.Message);

        // Tx2: Handle BPMN Error در یک transaction جدا
        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            var process = await _uow.Processes.GetByIdAsync(processId, trxCt);
            if (process == null)
            {
                _logger.LogWarning("[ORCHESTRATOR] Process not found for BPMN error handling. ProcessId={ProcessId}", processId);
                return;
            }

            var token = await _uow.Tokens.GetByIdAsync(tokenId, trxCt);
            if (token == null)
            {
                _logger.LogWarning("[ORCHESTRATOR] Token not found for BPMN error handling. TokenId={TokenId}", tokenId);
                return;
            }

            // ✅ Error Boundary Handling Flow via Subscription Manager
            // Publish ErrorRaisedEvent - Subscription Manager will handle lookup and execution
            _logger.LogInformation(
                "[ORCHESTRATOR] Publishing ErrorRaisedEvent for subscription-based error handling. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId} ErrorCode={ErrorCode} ScopeId={ScopeId}",
                processId,
                tokenId,
                token.CurrentElementId,
                bex.Code,
                token.ScopeId);

            var errorRaisedEvent = new ErrorRaisedEvent(
                ProcessId: processId,
                TokenId: tokenId,
                ElementId: token.CurrentElementId,
                ErrorCode: bex.Code,
                ErrorMessage: bex.Message,
                ScopeId: token.ScopeId,
                OccurredAtUtc: DateTime.UtcNow);

            // ✅ Error Boundary Handling Flow via Subscription Manager
            // Publish ErrorRaisedEvent - BoundarySubscriptionManager will handle subscription lookup and execution
            await _mediator.Publish(errorRaisedEvent, trxCt);
            
            // Save changes (BoundarySubscriptionManager may have updated subscriptions)
            await _uow.SaveChangesAsync(trxCt);
            
            // Reload token to check current state after error handling
            token = await _uow.Tokens.GetByIdAsync(tokenId, trxCt);
            if (token == null)
            {
                _logger.LogWarning("[ORCHESTRATOR] Token not found after error handling. TokenId={TokenId}", tokenId);
                return;
            }

            // Check if error was handled by BoundarySubscriptionManager
            // If handled, token would have been moved/terminated by boundary executor
            // If not handled, token is still at the same element - handle as unhandled
            var wasHandled = token.CurrentElementId != errorRaisedEvent.ElementId 
                          || token.State == TokenState.Terminated 
                          || token.State == TokenState.Completed;

            if (!wasHandled)
            {
                // ✅ Trace-First Token Semantics: No error handler found - unhandled BPMN error
                // Convert all executable tokens to trace tokens and fail the process
                _logger.LogWarning(
                    "[ORCHESTRATOR] No error boundary found for BPMN error. Converting all tokens to trace tokens and failing process. ProcessId={ProcessId} TokenId={TokenId} ErrorCode={ErrorCode} ElementId={ElementId}",
                    processId,
                    tokenId,
                    bex.Code,
                    token.CurrentElementId);

                // ✅ Step 1: Convert all active executable tokens to trace tokens
                var allTokens = await _uow.Tokens.GetByProcessIdAsync(processId, trxCt);
                var executableTokens = allTokens
                    .Where(t => t.IsExecutable && TokenExtensions.IsActiveToken(t))
                    .ToList();

                _logger.LogInformation(
                    "[ORCHESTRATOR] Converting {Count} executable tokens to trace tokens for unhandled error. ProcessId={ProcessId}",
                    executableTokens.Count,
                    processId);

                foreach (var t in executableTokens)
                {
                    _logger.LogDebug(
                        "[ORCHESTRATOR] Converting token to trace token. TokenId={TokenId} ElementId={ElementId} State={State}",
                        t.Id,
                        t.CurrentElementId,
                        t.State);

                    // Convert to trace token: mark as non-executable
                    t.MarkNonExecutable($"Unhandled BPMN error: {bex.Code} - converted to trace token");

                    // If token is waiting (e.g., at a join), resume it so it can continue as trace token
                    if (t.State == TokenState.Waiting)
                    {
                        t.ResumeWithoutProcessing();
                    }
                }

                // ✅ Step 2: Cancel all subscriptions (no error handler means no compensation path)
                var allSubscriptions = await _uow.BoundarySubscriptions.GetByProcessIdAsync(processId, trxCt);
                var activeSubscriptions = allSubscriptions
                    .Where(s => s.State == SubscriptionState.Active)
                    .ToList();

                _logger.LogInformation(
                    "[ORCHESTRATOR] Canceling {Count} active subscriptions for unhandled error. ProcessId={ProcessId}",
                    activeSubscriptions.Count,
                    processId);

                foreach (var sub in activeSubscriptions)
                {
                    sub.Cancel();
                    await _uow.BoundarySubscriptions.UpdateAsync(sub, trxCt);

                    // Cancel external job if exists
                    if (!string.IsNullOrWhiteSpace(sub.ExternalJobKey))
                    {
                        try
                        {
                            await _timerScheduler.CancelAsync(sub.ExternalJobKey, trxCt);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                ex,
                                "[ORCHESTRATOR] Failed to cancel external job. SubscriptionId={SubscriptionId} JobKey={JobKey}",
                                sub.Id,
                                sub.ExternalJobKey);
                        }
                    }
                }

                // ✅ Step 3: Create incident and fail token/process
                var incident = await _incidentService.CreateBpmnErrorAsync(
                    processId,
                    tokenId,
                    token.CurrentElementId,
                    bex.Code,
                    bex.Message,
                    trxCt);

                // Fail token with incident
                token.Fail(
                    $"Unhandled BPMN Error: {bex.Code} - {bex.Message}",
                    ErrorType.BpmnError,
                    errorCode: bex.Code,
                    incident.Id);

                // Fail the process (unhandled error means process cannot continue)
                process.Fail($"Unhandled BPMN Error: {bex.Code} - {bex.Message}");

                await _uow.SaveChangesAsync(trxCt);

                _logger.LogInformation(
                    "[ORCHESTRATOR] ✅ Unhandled BPMN error handled with Trace-First semantics. ProcessId={ProcessId} TokenId={TokenId} ErrorCode={ErrorCode} IncidentId={IncidentId} ConvertedToTrace={TraceCount}",
                    processId,
                    tokenId,
                    bex.Code,
                    incident.Id,
                    executableTokens.Count);
            }
        }, ct);

        // هیچ throw نمی‌کنیم - exception handle شده است
    }

    /// <summary>
    /// Handle Technical Failure در یک transaction جدا
    /// </summary>
    private async Task HandleTechnicalFailureAsync(
        Guid processId,
        Guid tokenId,
        TokenExecutionException tex,
        CancellationToken ct)
    {
        _logger.LogError(
            tex.InnerException ?? tex,
            "[ORCHESTRATOR] Technical failure occurred. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId}",
            processId,
            tokenId,
            tex.ElementId);

        // Tx2: ثبت Incident و Fail کردن Token در یک transaction جدا
        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            var process = await _uow.Processes.GetByIdAsync(processId, trxCt);
            if (process == null)
            {
                _logger.LogWarning("[ORCHESTRATOR] Process not found for technical failure handling. ProcessId={ProcessId}", processId);
                return;
            }

            var token = await _uow.Tokens.GetByIdAsync(tokenId, trxCt);
            if (token == null)
            {
                _logger.LogWarning("[ORCHESTRATOR] Token not found for technical failure handling. TokenId={TokenId}", tokenId);
                return;
            }

            // ایجاد Incident با stack trace
            var stackTrace = tex.InnerException?.ToString() ?? tex.StackTrace ?? string.Empty;
            var incident = await _incidentService.CreateTechnicalFailureAsync(
                processId,
                tokenId,
                tex.ElementId,
                tex.Message,
                stackTrace,
                trxCt);

            // Fail Token with Incident
            token.Fail(
                $"Technical failure: {tex.Message}",
                ErrorType.TechnicalFailure,
                errorCode: null,
                incident.Id);

            // SaveChanges is handled by TransactionService automatically
            // Note: IncidentService already saved the incident, but TransactionService will save token changes

            _logger.LogInformation(
                "[ORCHESTRATOR] Technical failure handled. ProcessId={ProcessId} TokenId={TokenId} IncidentId={IncidentId}",
                processId,
                tokenId,
                incident.Id);
        }, ct);

        // هیچ throw نمی‌کنیم - exception handle شده است
    }
}