using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Executor واحد برای semantics Boundary Event (BPMN2-سازگار)
/// این کلاس مسئولیت اجرای منطق interrupting/non-interrupting را دارد
/// </summary>
public sealed class BoundaryEventExecutor : IBoundaryEventExecutor
{
    private readonly IUnitOfWork _uow;
    private readonly IBpmnRuntimeContextFactory _ctxFactory;
    private readonly IBoundaryTimerScheduler _timerScheduler;
    private readonly ILogger<BoundaryEventExecutor> _logger;

    public BoundaryEventExecutor(
        IUnitOfWork uow,
        IBpmnRuntimeContextFactory ctxFactory,
        IBoundaryTimerScheduler timerScheduler,
        ILogger<BoundaryEventExecutor> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _ctxFactory = ctxFactory ?? throw new ArgumentNullException(nameof(ctxFactory));
        _timerScheduler = timerScheduler ?? throw new ArgumentNullException(nameof(timerScheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(Guid subscriptionId, CancellationToken ct)
    {
        try
        {
            _logger.LogDebug(
                "[BOUNDARY-EXECUTOR] Starting boundary event execution. SubscriptionId={SubscriptionId}",
                subscriptionId);

            // Load subscription + token + process
            var subscription = await _uow.BoundarySubscriptions.GetByIdAsync(subscriptionId, ct);
            if (subscription == null)
            {
                _logger.LogWarning(
                    "[BOUNDARY-EXECUTOR] ❌ Subscription not found. SubscriptionId={SubscriptionId}",
                    subscriptionId);
                return;
            }

            _logger.LogDebug(
                "[BOUNDARY-EXECUTOR] Subscription loaded. SubscriptionId={SubscriptionId} Kind={Kind} IsInterrupting={IsInterrupting} State={State} ProcessId={ProcessId} TokenId={TokenId}",
                subscription.Id,
                subscription.Kind,
                subscription.IsInterrupting,
                subscription.State,
                subscription.ProcessId,
                subscription.TokenId);

            if (subscription.State != SubscriptionState.Active)
            {
                _logger.LogWarning(
                    "[BOUNDARY-EXECUTOR] ❌ Subscription is not Active. SubscriptionId={SubscriptionId} State={State}",
                    subscriptionId,
                    subscription.State);
                return;
            }

            var token = await _uow.Tokens.GetByIdAsync(subscription.TokenId, ct);
            if (token == null)
            {
                _logger.LogWarning(
                    "[BOUNDARY-EXECUTOR] ❌ Token not found. SubscriptionId={SubscriptionId} TokenId={TokenId}",
                    subscriptionId,
                    subscription.TokenId);
                return;
            }

            _logger.LogDebug(
                "[BOUNDARY-EXECUTOR] Token loaded. TokenId={TokenId} State={State} CurrentElementId={CurrentElementId} ActivityInstanceId={ActivityInstanceId}",
                token.Id,
                token.State,
                token.CurrentElementId,
                token.ActivityInstanceId);

            // Guard: چک کن token هنوز روی همان element است (activity تمام نشده)
            if (token.CurrentElementId != subscription.AttachedToElementId)
            {
                _logger.LogInformation(
                    "[BOUNDARY-EXECUTOR] ⚠️ Token moved away from attached element. Canceling subscription. SubscriptionId={SubscriptionId} TokenId={TokenId} AttachedTo={AttachedTo} CurrentElement={CurrentElement}",
                    subscriptionId,
                    token.Id,
                    subscription.AttachedToElementId,
                    token.CurrentElementId);

                subscription.Cancel();
                await _uow.BoundarySubscriptions.UpdateAsync(subscription, ct);
                return;
            }

            var process = await _uow.Processes.GetByIdAsync(subscription.ProcessId, ct);
            if (process == null)
            {
                _logger.LogWarning(
                    "[BOUNDARY-EXECUTOR] ❌ Process not found. SubscriptionId={SubscriptionId} ProcessId={ProcessId}",
                    subscriptionId,
                    subscription.ProcessId);
                return;
            }

            _logger.LogDebug(
                "[BOUNDARY-EXECUTOR] Process loaded. ProcessId={ProcessId} State={State}",
                process.Id,
                process.State);

            // Mark subscription as triggered
            _logger.LogDebug(
                "[BOUNDARY-EXECUTOR] Marking subscription as triggered. SubscriptionId={SubscriptionId}",
                subscriptionId);

            subscription.MarkTriggered();
            await _uow.BoundarySubscriptions.UpdateAsync(subscription, ct);

            var ctx = await _ctxFactory.CreateAsync(process, ct);
            var boundaryEvent = ctx.Model.GetElementById(ctx.BpmnProcessId, subscription.BoundaryEventId) as BpmnBoundaryEvent;
            if (boundaryEvent == null)
            {
                _logger.LogError(
                    "[BOUNDARY-EXECUTOR] ❌ Boundary event not found in model. SubscriptionId={SubscriptionId} BoundaryEventId={BoundaryEventId}",
                    subscriptionId,
                    subscription.BoundaryEventId);
                return;
            }

            _logger.LogDebug(
                "[BOUNDARY-EXECUTOR] Boundary event loaded from model. BoundaryEventId={BoundaryEventId} AttachedTo={AttachedTo}",
                boundaryEvent.id,
                boundaryEvent.attachedToRef?.Name);

            // Get outgoing flow from boundary event
            var outgoingFlows = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, subscription.BoundaryEventId);

            _logger.LogDebug(
                "[BOUNDARY-EXECUTOR] Found {Count} outgoing flows from boundary event. BoundaryEventId={BoundaryEventId}",
                outgoingFlows.Count,
                subscription.BoundaryEventId);

            if (outgoingFlows.Count == 0)
            {
                _logger.LogWarning(
                    "[BOUNDARY-EXECUTOR] ❌ No outgoing flows from boundary event. SubscriptionId={SubscriptionId} BoundaryEventId={BoundaryEventId}",
                    subscriptionId,
                    subscription.BoundaryEventId);
                return;
            }

            var targetElementId = outgoingFlows[0].targetRef;
            if (string.IsNullOrWhiteSpace(targetElementId))
            {
                _logger.LogError(
                    "[BOUNDARY-EXECUTOR] ❌ Outgoing flow has no target. SubscriptionId={SubscriptionId} BoundaryEventId={BoundaryEventId}",
                    subscriptionId,
                    subscription.BoundaryEventId);
                return;
            }

            _logger.LogDebug(
                "[BOUNDARY-EXECUTOR] Target element determined. TargetElementId={TargetElementId} ViaFlowId={ViaFlowId} IsInterrupting={IsInterrupting}",
                targetElementId,
                outgoingFlows[0].id,
                subscription.IsInterrupting);

        // ✅ Trace-First Token Semantics: Error boundary events always follow trace-first semantics
        // regardless of interrupting/non-interrupting setting (error boundaries are inherently interrupting for error handling)
        if (subscription.Kind == BoundaryKind.Error)
        {
            _logger.LogDebug(
                "[BOUNDARY-EXECUTOR] Executing ERROR boundary event with Trace-First semantics. SubscriptionId={SubscriptionId}",
                subscriptionId);
            await ExecuteErrorBoundaryWithTraceFirstAsync(process, token, subscription, targetElementId, outgoingFlows[0].id, ct);
        }
        else if (subscription.IsInterrupting)
        {
            _logger.LogDebug(
                "[BOUNDARY-EXECUTOR] Executing INTERRUPTING boundary event. SubscriptionId={SubscriptionId}",
                subscriptionId);
            await ExecuteInterruptingAsync(process, token, subscription, targetElementId, outgoingFlows[0].id, ct);
        }
        else
        {
            _logger.LogDebug(
                "[BOUNDARY-EXECUTOR] Executing NON-INTERRUPTING boundary event. SubscriptionId={SubscriptionId}",
                subscriptionId);
            await ExecuteNonInterruptingAsync(process, token, subscription, targetElementId, outgoingFlows[0].id, ct);
        }
        }
        catch (Exception ex)
        {
            // ✅ Trace-First Token Semantics: If boundary event execution fails, convert tokens to trace tokens
            // Boundary event execution should never fail tokens - maintain trace-first semantics
            _logger.LogError(
                ex,
                "[BOUNDARY-EXECUTOR] ❌ Boundary event execution failed. Converting tokens to trace tokens. SubscriptionId={SubscriptionId}",
                subscriptionId);

            try
            {
                // Emergency trace-first conversion
                await ExecuteBoundaryEventWithTraceFirstFallbackAsync(subscriptionId, ex, ct);
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(
                    fallbackEx,
                    "[BOUNDARY-EXECUTOR] ❌ Emergency trace-first fallback also failed. SubscriptionId={SubscriptionId}",
                    subscriptionId);
            }
        }
    }

    /// <summary>
    /// Emergency fallback for boundary event execution failures.
    /// Converts all tokens in scope to trace tokens to maintain trace-first semantics.
    /// </summary>
    private async Task ExecuteBoundaryEventWithTraceFirstFallbackAsync(Guid subscriptionId, Exception originalException, CancellationToken ct)
    {
        _logger.LogWarning(
            "[BOUNDARY-EXECUTOR] Executing emergency trace-first fallback for failed boundary event. SubscriptionId={SubscriptionId}",
            subscriptionId);

        try
        {
            // Load subscription
            var subscription = await _uow.BoundarySubscriptions.GetByIdAsync(subscriptionId, ct);
            if (subscription == null || subscription.State != SubscriptionState.Active)
                return;

            // Load token and process
            var token = await _uow.Tokens.GetByIdAsync(subscription.TokenId, ct);
            var process = await _uow.Processes.GetByIdAsync(subscription.ProcessId, ct);
            if (token == null || process == null)
                return;

            // Mark subscription as triggered (even though execution failed)
            subscription.MarkTriggered();
            await _uow.BoundarySubscriptions.UpdateAsync(subscription, ct);

            // ✅ Emergency trace-first conversion: convert all executable tokens in scope to trace tokens
            var scopeId = token.ScopeId ?? token.Id;
            var allTokens = await _uow.Tokens.GetByProcessIdAsync(process.Id, ct);
            var scopeTokens = allTokens.Where(t => t.ScopeId == scopeId).ToList();

            var executableTokensInScope = scopeTokens
                .Where(t => t.IsExecutable && IsActiveToken(t))
                .ToList();

            _logger.LogWarning(
                "[BOUNDARY-EXECUTOR] Emergency converting {Count} executable tokens to trace tokens. ScopeId={ScopeId}",
                executableTokensInScope.Count,
                scopeId);

            foreach (var t in executableTokensInScope)
            {
                t.MarkNonExecutable($"Emergency trace conversion - boundary event failed: {originalException.Message}");
                if (t.State == TokenState.Waiting)
                {
                    t.ResumeWithoutProcessing();
                }
            }

            // Cancel all related subscriptions
            var allSubscriptions = await _uow.BoundarySubscriptions.GetByProcessIdAsync(process.Id, ct);
            var subscriptionsToCancel = allSubscriptions
                .Where(s => s.State == SubscriptionState.Active
                         && s.Id != subscription.Id
                         && scopeTokens.Select(t => t.Id).Contains(s.TokenId))
                .ToList();

            foreach (var sub in subscriptionsToCancel)
            {
                sub.Cancel();
                await _uow.BoundarySubscriptions.UpdateAsync(sub, ct);

                // Cancel external jobs
                if (!string.IsNullOrWhiteSpace(sub.ExternalJobKey))
                {
                    try
                    {
                        await _timerScheduler.CancelAsync(sub.ExternalJobKey, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[BOUNDARY-EXECUTOR] Failed to cancel external job in fallback. SubscriptionId={SubscriptionId}", sub.Id);
                    }
                }
            }

            await _uow.SaveChangesAsync(ct);

            _logger.LogWarning(
                "[BOUNDARY-EXECUTOR] ✅ Emergency trace-first fallback completed. SubscriptionId={SubscriptionId} ConvertedToTrace={TraceCount}",
                subscriptionId,
                executableTokensInScope.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[BOUNDARY-EXECUTOR] ❌ Emergency trace-first fallback failed completely. SubscriptionId={SubscriptionId}",
                subscriptionId);
        }
    }

    /// <summary>
    /// Execute error boundary event with Trace-First Token Semantics:
    /// 1. Convert all active executable tokens in the same ScopeId to trace tokens
    /// 2. Cancel all subscriptions related to that ScopeId
    /// 3. Create new executable token on boundary event's outgoing flow
    /// 4. Ensure no tokens fail - only trace conversion and continuation
    /// </summary>
    private async Task ExecuteErrorBoundaryWithTraceFirstAsync(
        Process process,
        Token token,
        BoundarySubscription subscription,
        string targetElementId,
        string? viaFlowId,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "[BOUNDARY-EXECUTOR] Executing ERROR boundary event with Trace-First semantics. SubscriptionId={SubscriptionId} TokenId={TokenId} BoundaryEventId={BoundaryEventId} TargetElementId={TargetElementId} ScopeId={ScopeId}",
            subscription.Id,
            token.Id,
            subscription.BoundaryEventId,
            targetElementId,
            token.ScopeId);

        // ✅ Trace-First Token Semantics: Ensure token has ScopeId for cycle-aware correlation
        if (!token.ScopeId.HasValue)
        {
            // Fallback: use ActivityInstanceId or token Id as scope
            var fallbackScopeId = token.ActivityInstanceId ?? token.Id;
            token.SetScope(fallbackScopeId);
            _logger.LogWarning(
                "[BOUNDARY-EXECUTOR] Token has no ScopeId, using fallback. TokenId={TokenId} FallbackScopeId={ScopeId}",
                token.Id,
                fallbackScopeId);
        }

        var scopeId = token.ScopeId!.Value;

        // ✅ Step 1: Convert all active executable tokens in the same ScopeId to trace tokens
        var allTokens = await _uow.Tokens.GetByProcessIdAsync(process.Id, ct);
        var scopeTokens = allTokens
            .Where(t => t.ScopeId == scopeId && t.Id != token.Id)
            .ToList();

        var executableTokensInScope = scopeTokens
            .Where(t => t.IsExecutable && IsActiveToken(t))
            .ToList();

        _logger.LogInformation(
            "[BOUNDARY-EXECUTOR] Found {Count} executable tokens in scope to convert to trace tokens. ScopeId={ScopeId}",
            executableTokensInScope.Count,
            scopeId);

        foreach (var t in executableTokensInScope)
        {
            _logger.LogDebug(
                "[BOUNDARY-EXECUTOR] Converting token to trace token. TokenId={TokenId} ElementId={ElementId} State={State}",
                t.Id,
                t.CurrentElementId,
                t.State);

            // Convert to trace token: mark as non-executable
            // Trace tokens will continue moving through the graph to End without executing semantics
            t.MarkNonExecutable($"Converted to trace token by error boundary: {subscription.BoundaryEventId}");

            // If token is waiting (e.g., at a join), resume it so it can continue as trace token
            if (t.State == TokenState.Waiting)
            {
                t.ResumeWithoutProcessing();
            }
        }

        // ✅ Step 2: Cancel all subscriptions related to this ScopeId
        var scopeTokenIds = scopeTokens.Select(t => t.Id).ToList();
        scopeTokenIds.Add(token.Id); // Include current token

        var allSubscriptions = await _uow.BoundarySubscriptions.GetByProcessIdAsync(process.Id, ct);
        var subscriptionsToCancel = allSubscriptions
            .Where(s => s.State == SubscriptionState.Active
                     && s.Id != subscription.Id
                     && scopeTokenIds.Contains(s.TokenId))
            .ToList();

        _logger.LogInformation(
            "[BOUNDARY-EXECUTOR] Found {Count} subscriptions to cancel for scope. ScopeId={ScopeId}",
            subscriptionsToCancel.Count,
            scopeId);

        foreach (var sub in subscriptionsToCancel)
        {
            sub.Cancel();
            await _uow.BoundarySubscriptions.UpdateAsync(sub, ct);

            _logger.LogDebug(
                "[BOUNDARY-EXECUTOR] Canceled subscription. SubscriptionId={SubscriptionId} TokenId={TokenId} BoundaryEventId={BoundaryEventId}",
                sub.Id,
                sub.TokenId,
                sub.BoundaryEventId);

            // Cancel external job if exists (e.g., timer subscriptions)
            if (!string.IsNullOrWhiteSpace(sub.ExternalJobKey))
            {
                try
                {
                    await _timerScheduler.CancelAsync(sub.ExternalJobKey, ct);
                    _logger.LogDebug(
                        "[BOUNDARY-EXECUTOR] Canceled external job. SubscriptionId={SubscriptionId} JobKey={JobKey}",
                        sub.Id,
                        sub.ExternalJobKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "[BOUNDARY-EXECUTOR] Failed to cancel external job. SubscriptionId={SubscriptionId} JobKey={JobKey}",
                        sub.Id,
                        sub.ExternalJobKey);
                }
            }
        }

        // ✅ Step 3: Terminate current token (it will be replaced by boundary token)
        // Note: For error boundaries, we terminate the current token and replace it with boundary token
        token.Terminate($"Interrupted by error boundary event: {subscription.BoundaryEventId}");
        process.RemoveToken(token.Id);

        // ✅ Step 4: Create new executable token on boundary event's outgoing flow
        var boundaryToken = new Token(process.Id, targetElementId, new[] { token.Id });

        // Preserve ActivityInstanceId if exists (for proper cancellation semantics)
        if (token.ActivityInstanceId.HasValue)
        {
            boundaryToken.SetActivityInstance(token.ActivityInstanceId.Value);
        }

        // Copy variables from original token
        foreach (var kv in token.Variables)
        {
            boundaryToken.SetVariable(kv.Key, kv.Value);
        }

        // Boundary token is executable (continues compensation/replacement path)
        // Note: IsExecutable defaults to true, so no need to set it explicitly

        await _uow.Tokens.AddAsync(boundaryToken, ct);
        process.AddToken(boundaryToken.Id);
        boundaryToken.SetArrivedVia(viaFlowId);
        boundaryToken.Activate(); // Creates TokenProcessingRequestedEvent

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[BOUNDARY-EXECUTOR] ✅ ERROR boundary event executed with Trace-First semantics. SubscriptionId={SubscriptionId} OldTokenId={OldTokenId} NewTokenId={NewTokenId} ConvertedToTrace={TraceCount}",
            subscription.Id,
            token.Id,
            boundaryToken.Id,
            executableTokensInScope.Count);
    }

    /// <summary>
    /// Execute interrupting boundary event with Trace-First Token Semantics:
    /// 1. Convert all active executable tokens in the same ScopeId to trace tokens
    /// 2. Cancel all subscriptions related to that ScopeId
    /// 3. Create new executable token on boundary event's outgoing flow
    /// </summary>
    private async Task ExecuteInterruptingAsync(
        Process process,
        Token token,
        BoundarySubscription subscription,
        string targetElementId,
        string? viaFlowId,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "[BOUNDARY-EXECUTOR] Executing interrupting boundary event with Trace-First semantics. SubscriptionId={SubscriptionId} TokenId={TokenId} BoundaryEventId={BoundaryEventId} TargetElementId={TargetElementId} ScopeId={ScopeId}",
            subscription.Id,
            token.Id,
            subscription.BoundaryEventId,
            targetElementId,
            token.ScopeId);

        // ✅ Trace-First Token Semantics: Ensure token has ScopeId for cycle-aware correlation
        if (!token.ScopeId.HasValue)
        {
            // Fallback: use ActivityInstanceId or token Id as scope
            var fallbackScopeId = token.ActivityInstanceId ?? token.Id;
            token.SetScope(fallbackScopeId);
            _logger.LogWarning(
                "[BOUNDARY-EXECUTOR] Token has no ScopeId, using fallback. TokenId={TokenId} FallbackScopeId={ScopeId}",
                token.Id,
                fallbackScopeId);
        }

        var scopeId = token.ScopeId!.Value;

        // ✅ Step 1: Convert all active executable tokens in the same ScopeId to trace tokens
        var allTokens = await _uow.Tokens.GetByProcessIdAsync(process.Id, ct);
        var scopeTokens = allTokens
            .Where(t => t.ScopeId == scopeId && t.Id != token.Id)
            .ToList();

        var executableTokensInScope = scopeTokens
            .Where(t => t.IsExecutable && IsActiveToken(t))
            .ToList();

        _logger.LogInformation(
            "[BOUNDARY-EXECUTOR] Found {Count} executable tokens in scope to convert to trace tokens. ScopeId={ScopeId}",
            executableTokensInScope.Count,
            scopeId);

        foreach (var t in executableTokensInScope)
        {
            _logger.LogDebug(
                "[BOUNDARY-EXECUTOR] Converting token to trace token. TokenId={TokenId} ElementId={ElementId} State={State}",
                t.Id,
                t.CurrentElementId,
                t.State);

            // Convert to trace token: mark as non-executable
            // Trace tokens will continue moving through the graph to End without executing semantics
            t.MarkNonExecutable($"Converted to trace token by error boundary: {subscription.BoundaryEventId}");
            
            // If token is waiting (e.g., at a join), resume it so it can continue as trace token
            if (t.State == TokenState.Waiting)
            {
                t.ResumeWithoutProcessing();
            }
        }

        // ✅ Step 2: Cancel all subscriptions related to this ScopeId
        var scopeTokenIds = scopeTokens.Select(t => t.Id).ToList();
        scopeTokenIds.Add(token.Id); // Include current token

        var allSubscriptions = await _uow.BoundarySubscriptions.GetByProcessIdAsync(process.Id, ct);
        var subscriptionsToCancel = allSubscriptions
            .Where(s => s.State == SubscriptionState.Active 
                     && s.Id != subscription.Id 
                     && scopeTokenIds.Contains(s.TokenId))
            .ToList();

        _logger.LogInformation(
            "[BOUNDARY-EXECUTOR] Found {Count} subscriptions to cancel for scope. ScopeId={ScopeId}",
            subscriptionsToCancel.Count,
            scopeId);

        foreach (var sub in subscriptionsToCancel)
        {
            sub.Cancel();
            await _uow.BoundarySubscriptions.UpdateAsync(sub, ct);
            
            _logger.LogDebug(
                "[BOUNDARY-EXECUTOR] Canceled subscription. SubscriptionId={SubscriptionId} TokenId={TokenId} BoundaryEventId={BoundaryEventId}",
                sub.Id,
                sub.TokenId,
                sub.BoundaryEventId);

            // Cancel external job if exists (e.g., timer subscriptions)
            if (!string.IsNullOrWhiteSpace(sub.ExternalJobKey))
            {
                try
                {
                    await _timerScheduler.CancelAsync(sub.ExternalJobKey, ct);
                    _logger.LogDebug(
                        "[BOUNDARY-EXECUTOR] Canceled external job. SubscriptionId={SubscriptionId} JobKey={JobKey}",
                        sub.Id,
                        sub.ExternalJobKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "[BOUNDARY-EXECUTOR] Failed to cancel external job. SubscriptionId={SubscriptionId} JobKey={JobKey}",
                        sub.Id,
                        sub.ExternalJobKey);
                }
            }
        }

        // ✅ Step 3: Terminate current token (it will be replaced by boundary token)
        token.Terminate($"Interrupted by error boundary event: {subscription.BoundaryEventId}");
        process.RemoveToken(token.Id);

        // ✅ Step 4: Create new executable token on boundary event's outgoing flow
        var boundaryToken = new Token(process.Id, targetElementId, new[] { token.Id });
        
        // Preserve ActivityInstanceId if exists (for proper cancellation semantics)
        if (token.ActivityInstanceId.HasValue)
        {
            boundaryToken.SetActivityInstance(token.ActivityInstanceId.Value);
        }
        
        // Copy variables from original token
        foreach (var kv in token.Variables)
        {
            boundaryToken.SetVariable(kv.Key, kv.Value);
        }

        // Boundary token is executable (continues compensation/replacement path)
        // Note: IsExecutable defaults to true, so no need to set it explicitly

        await _uow.Tokens.AddAsync(boundaryToken, ct);
        process.AddToken(boundaryToken.Id);
        boundaryToken.SetArrivedVia(viaFlowId);
        boundaryToken.Activate(); // Creates TokenProcessingRequestedEvent

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[BOUNDARY-EXECUTOR] ✅ Interrupting boundary event executed with Trace-First semantics. SubscriptionId={SubscriptionId} OldTokenId={OldTokenId} NewTokenId={NewTokenId} ConvertedToTrace={TraceCount}",
            subscription.Id,
            token.Id,
            boundaryToken.Id,
            executableTokensInScope.Count);
    }

    /// <summary>
    /// Determines if a token is "active" (should be converted to trace token).
    /// Active tokens are in Created/Active/Waiting states.
    /// </summary>
    private static bool IsActiveToken(Token token)
    {
        return token.State is TokenState.Created
            or TokenState.Active
            or TokenState.Waiting;
    }

    /// <summary>
    /// Execute non-interrupting boundary event:
    /// 1. Create token for boundary event path (بدون cancel کردن activity)
    /// 2. Token اصلی ادامه می‌دهد (parallel flow)
    /// </summary>
    private async Task ExecuteNonInterruptingAsync(
        Process process,
        Token token,
        BoundarySubscription subscription,
        string targetElementId,
        string? viaFlowId,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "[BOUNDARY-EXECUTOR] Executing non-interrupting boundary event. SubscriptionId={SubscriptionId} TokenId={TokenId} BoundaryEventId={BoundaryEventId} TargetElementId={TargetElementId}",
            subscription.Id,
            token.Id,
            subscription.BoundaryEventId,
            targetElementId);

        // Create token for boundary event path (بدون cancel کردن token اصلی)
        var boundaryToken = new Token(process.Id, targetElementId, new[] { token.Id });
        
        // Copy variables from original token
        foreach (var kv in token.Variables)
        {
            boundaryToken.SetVariable(kv.Key, kv.Value);
        }

        await _uow.Tokens.AddAsync(boundaryToken, ct);
        process.AddToken(boundaryToken.Id);
        boundaryToken.SetArrivedVia(viaFlowId);
        boundaryToken.Activate(); // این TokenProcessingRequestedEvent ایجاد می‌کند

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[BOUNDARY-EXECUTOR] Non-interrupting boundary event executed. SubscriptionId={SubscriptionId} OriginalTokenId={OriginalTokenId} NewTokenId={NewTokenId}",
            subscription.Id,
            token.Id,
            boundaryToken.Id);
    }
}
