using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Exceptions;
using Novin.Bpmn.Engine.Domain.ValueObjects;

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
    private readonly ILogger<TokenProcessingOrchestrator> _logger;

    public TokenProcessingOrchestrator(
        IUnitOfWork uow,
        IBpmnRuntimeContextFactory ctxFactory,
        ITokenExecutionDispatcher dispatcher,
        IIncidentService incidentService,
        IBpmnErrorBoundaryFinder errorBoundaryFinder,
        IBoundaryEventExecutor boundaryEventExecutor,
        ILogger<TokenProcessingOrchestrator> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _ctxFactory = ctxFactory ?? throw new ArgumentNullException(nameof(ctxFactory));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _incidentService = incidentService ?? throw new ArgumentNullException(nameof(incidentService));
        _errorBoundaryFinder = errorBoundaryFinder ?? throw new ArgumentNullException(nameof(errorBoundaryFinder));
        _boundaryEventExecutor = boundaryEventExecutor ?? throw new ArgumentNullException(nameof(boundaryEventExecutor));
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

            // Create BPMN runtime context to search for error boundaries
            _logger.LogDebug(
                "[ORCHESTRATOR] Creating BPMN runtime context for error boundary search. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId}",
                processId,
                tokenId,
                token.CurrentElementId);

            var ctx = await _ctxFactory.CreateAsync(process, trxCt);

            // Step 1: Try to find Error Boundary attached to current element
            _logger.LogDebug(
                "[ORCHESTRATOR] Step 1: Searching for error boundary attached to element. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId} ErrorCode={ErrorCode}",
                processId,
                tokenId,
                token.CurrentElementId,
                bex.Code);

            var errorBoundaryId = _errorBoundaryFinder.FindErrorBoundary(ctx, token.CurrentElementId, bex.Code);

            // Step 2: If no boundary found, try to find Error EventSubprocess
            if (errorBoundaryId == null)
            {
                _logger.LogDebug(
                    "[ORCHESTRATOR] Step 2: No error boundary found, searching for error event subprocess. ProcessId={ProcessId} TokenId={TokenId} ErrorCode={ErrorCode}",
                    processId,
                    tokenId,
                    bex.Code);

                errorBoundaryId = _errorBoundaryFinder.FindErrorEventSubprocess(ctx, bex.Code);
            }

            if (errorBoundaryId != null)
            {
                // Error handler found: use boundary event executor for consistent semantics
                _logger.LogInformation(
                    "[ORCHESTRATOR] Error boundary found. Using boundary event executor. ProcessId={ProcessId} TokenId={TokenId} ErrorBoundaryId={ErrorBoundaryId} ErrorCode={ErrorCode}",
                    processId,
                    tokenId,
                    errorBoundaryId,
                    bex.Code);

                // Find or create subscription for error boundary
                // Note: Error boundary is synchronous, so we create a temporary subscription and execute immediately
                _logger.LogDebug(
                    "[ORCHESTRATOR] Looking for existing error boundary subscription. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId} ErrorBoundaryId={ErrorBoundaryId}",
                    processId,
                    tokenId,
                    token.CurrentElementId,
                    errorBoundaryId);

                var subscriptions = await _uow.BoundarySubscriptions.GetActiveByAttachedElementAsync(
                    processId,
                    token.CurrentElementId,
                    trxCt);

                _logger.LogDebug(
                    "[ORCHESTRATOR] Found {Count} active subscriptions for element. ProcessId={ProcessId} ElementId={ElementId}",
                    subscriptions.Count(),
                    processId,
                    token.CurrentElementId);

                var errorSubscription = subscriptions
                    .FirstOrDefault(s => s.BoundaryEventId == errorBoundaryId && s.Kind == BoundaryKind.Error);

                if (errorSubscription == null)
                {
                    _logger.LogDebug(
                        "[ORCHESTRATOR] Creating temporary subscription for error boundary. ProcessId={ProcessId} TokenId={TokenId} ErrorBoundaryId={ErrorBoundaryId} ErrorCode={ErrorCode} ActivityInstanceId={ActivityInstanceId}",
                        processId,
                        tokenId,
                        errorBoundaryId,
                        bex.Code,
                        token.ActivityInstanceId);

                    // Create temporary subscription for error boundary
                    errorSubscription = new BoundarySubscription(
                        processId,
                        tokenId,
                        token.CurrentElementId,
                        errorBoundaryId,
                        BoundaryKind.Error,
                        isInterrupting: true, // Error boundaries are always interrupting
                        dueAt: null,
                        correlationKey: null,
                        errorCode: bex.Code,
                        activityInstanceId: token.ActivityInstanceId); // Use ActivityInstanceId for proper cancellation

                    await _uow.BoundarySubscriptions.AddAsync(errorSubscription, trxCt);
                    
                    _logger.LogDebug(
                        "[ORCHESTRATOR] Temporary subscription created. SubscriptionId={SubscriptionId} ProcessId={ProcessId} TokenId={TokenId}",
                        errorSubscription.Id,
                        processId,
                        tokenId);
                }
                else
                {
                    _logger.LogDebug(
                        "[ORCHESTRATOR] Using existing error boundary subscription. SubscriptionId={SubscriptionId} ProcessId={ProcessId} TokenId={TokenId}",
                        errorSubscription.Id,
                        processId,
                        tokenId);
                }

                // Execute boundary event using shared executor
                _logger.LogDebug(
                    "[ORCHESTRATOR] Executing error boundary via executor. SubscriptionId={SubscriptionId} ProcessId={ProcessId} TokenId={TokenId}",
                    errorSubscription.Id,
                    processId,
                    tokenId);

                await _boundaryEventExecutor.ExecuteAsync(errorSubscription.Id, trxCt);

                _logger.LogInformation(
                    "[ORCHESTRATOR] Error boundary executed via executor. ProcessId={ProcessId} TokenId={TokenId} SubscriptionId={SubscriptionId} ErrorBoundaryId={ErrorBoundaryId}",
                    processId,
                    tokenId,
                    errorSubscription.Id,
                    errorBoundaryId);
            }
            else
            {
                // No error handler found: unhandled BPMN error
                // Create incident and fail token (process remains Running for operational handling)
                _logger.LogWarning(
                    "[ORCHESTRATOR] No error boundary found for BPMN error. Creating incident and failing token. ProcessId={ProcessId} TokenId={TokenId} ErrorCode={ErrorCode} ElementId={ElementId}",
                    processId,
                    tokenId,
                    bex.Code,
                    token.CurrentElementId);

                // Create BPMN Error incident
                var incident = await _incidentService.CreateBpmnErrorAsync(
                    processId,
                    tokenId,
                    token.CurrentElementId,
                    bex.Code,
                    bex.Message,
                    trxCt);

                // Fail token with incident (process remains Running - token stays in process)
                token.Fail(
                    $"Unhandled BPMN Error: {bex.Code} - {bex.Message}",
                    ErrorType.BpmnError,
                    errorCode: bex.Code,
                    incident.Id);

                // Note: We do NOT call process.RemoveToken() - process stays Running
                // This allows the process to be retried/resolved after incident handling

                await _uow.SaveChangesAsync(trxCt);

                _logger.LogInformation(
                    "[ORCHESTRATOR] Unhandled BPMN error handled. ProcessId={ProcessId} TokenId={TokenId} ErrorCode={ErrorCode} IncidentId={IncidentId}",
                    processId,
                    tokenId,
                    bex.Code,
                    incident.Id);
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