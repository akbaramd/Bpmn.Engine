// using MediatR;
// using Microsoft.Extensions.Logging;
// using Novin.Bpmn.Engine.Application.Common.Interfaces;
// using Novin.Bpmn.Engine.Application.Services;
// using Novin.Bpmn.Engine.Domain.Entities;
// using Novin.Bpmn.Engine.Domain.Events;
// using Novin.Bpmn.Engine.Domain.ValueObjects;
// using Novin.Bpmn.Models.Models;
//
// namespace Novin.Bpmn.Engine.Application.EventHandlers;
//
// /// <summary>
// /// Manager برای Boundary Subscription lifecycle
// /// وقتی token وارد یک activity می‌شود → subscription‌های boundary event را active می‌کند
// /// وقتی token از activity خارج می‌شود → subscription‌ها را cancel می‌کند
// /// </summary>
// using ProcessEntity = Novin.Bpmn.Engine.Domain.Entities.Process;
// public sealed class BoundarySubscriptionManager : 
//     INotificationHandler<TokenMovedEvent>,
//     INotificationHandler<TokenProcessedEvent>,
//     INotificationHandler<TokenTerminatedEvent>,
//     INotificationHandler<ErrorRaisedEvent>
// {
//     private readonly IUnitOfWork _uow;
//     private readonly IBpmnRuntimeContextFactory _ctxFactory;
//     private readonly IBoundaryTimerScheduler _timerScheduler;
//     private readonly IBoundaryEventExecutor _boundaryEventExecutor;
//     private readonly IClock _clock;
//     private readonly ILogger<BoundarySubscriptionManager> _logger;
//
//     public BoundarySubscriptionManager(
//         IUnitOfWork uow,
//         IBpmnRuntimeContextFactory ctxFactory,
//         IBoundaryTimerScheduler timerScheduler,
//         IBoundaryEventExecutor boundaryEventExecutor,
//         IClock clock,
//         ILogger<BoundarySubscriptionManager> logger)
//     {
//         _uow = uow ?? throw new ArgumentNullException(nameof(uow));
//         _ctxFactory = ctxFactory ?? throw new ArgumentNullException(nameof(ctxFactory));
//         _timerScheduler = timerScheduler ?? throw new ArgumentNullException(nameof(timerScheduler));
//         _boundaryEventExecutor = boundaryEventExecutor ?? throw new ArgumentNullException(nameof(boundaryEventExecutor));
//         _clock = clock ?? throw new ArgumentNullException(nameof(clock));
//         _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//     }
//
//     /// <summary>
//     /// Handle TokenMovedEvent: وقتی token وارد یک activity می‌شود یا از آن خارج می‌شود
//     /// </summary>
//     public async System.Threading.Tasks.Task Handle(TokenMovedEvent notification, CancellationToken ct)
//     {
//         await _uow.ExecuteInTransactionAsync(async txCt =>
//         {
//             var token = await _uow.Tokens.GetByIdAsync(notification.TokenId, txCt);
//             if (token == null)
//             {
//                 _logger.LogWarning(
//                     "[BOUNDARY-SUBSCRIPTION] Token not found. TokenId={TokenId}",
//                     notification.TokenId);
//                 return;
//             }
//
//             var process = await _uow.Processes.GetByIdAsync(notification.ProcessId, txCt);
//             if (process == null)
//             {
//                 _logger.LogWarning(
//                     "[BOUNDARY-SUBSCRIPTION] Process not found. ProcessId={ProcessId}",
//                     notification.ProcessId);
//                 return;
//             }
//
//             var ctx = await _ctxFactory.CreateAsync(process, txCt);
//
//             _logger.LogDebug(
//                 "[BOUNDARY-SUBSCRIPTION] Token moved. FromElementId={FromElementId} ToElementId={ToElementId} TokenId={TokenId} ProcessId={ProcessId}",
//                 notification.FromElementId,
//                 notification.ToElementId,
//                 notification.TokenId,
//                 notification.ProcessId);
//
//             // 1. Cancel subscriptions برای element قبلی (اگر activity بود)
//             _logger.LogDebug(
//                 "[BOUNDARY-SUBSCRIPTION] Step 1: Canceling subscriptions for previous element. FromElementId={FromElementId} TokenId={TokenId}",
//                 notification.FromElementId,
//                 notification.TokenId);
//
//             await CancelSubscriptionsForElementAsync(
//                 notification.ProcessId,
//                 notification.TokenId,
//                 notification.FromElementId,
//                 txCt);
//
//             // 2. Create subscriptions برای element جدید (اگر activity با boundary events باشد)
//             _logger.LogDebug(
//                 "[BOUNDARY-SUBSCRIPTION] Step 2: Creating subscriptions for new element. ToElementId={ToElementId} TokenId={TokenId}",
//                 notification.ToElementId,
//                 notification.TokenId);
//
//             await CreateSubscriptionsForElementAsync(
//                 process,
//                 token,
//                 notification.ToElementId,
//                 ctx,
//                 txCt);
//         }, ct);
//     }
//
//     /// <summary>
//     /// Handle TokenCompletedEvent: cancel همه subscription‌های token
//     /// </summary>
//     public async System.Threading.Tasks.Task Handle(TokenProcessedEvent notification, CancellationToken ct)
//     {
//         await CancelAllSubscriptionsForTokenAsync(notification.TokenId, ct);
//     }
//
//     /// <summary>
//     /// Handle TokenTerminatedEvent: cancel همه subscription‌های token
//     /// </summary>
//     public async System.Threading.Tasks.Task Handle(TokenTerminatedEvent notification, CancellationToken ct)
//     {
//         await CancelAllSubscriptionsForTokenAsync(notification.TokenId, ct);
//     }
//
//     /// <summary>
//     /// Handle ErrorRaisedEvent: Error Boundary Handling Flow via Subscription Manager
//     /// 1. Lookup subscriptions by ErrorCode and ScopeId (same scope first, then propagate to parent scopes)
//     /// 2. If match found: consume subscription, cancel other subscriptions in scope, execute boundary
//     /// 3. If no match: propagate to higher scopes or mark as unhandled
//     /// </summary>
//     public async System.Threading.Tasks.Task Handle(ErrorRaisedEvent notification, CancellationToken ct)
//     {
//         _logger.LogInformation(
//             "[ERROR-HANDLER] Error raised. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId} ErrorCode={ErrorCode} ScopeId={ScopeId}",
//             notification.ProcessId,
//             notification.TokenId,
//             notification.ElementId,
//             notification.ErrorCode,
//             notification.ScopeId);
//
//         await _uow.ExecuteInTransactionAsync(async txCt =>
//         {
//             var process = await _uow.Processes.GetByIdAsync(notification.ProcessId, txCt);
//             if (process == null)
//             {
//                 _logger.LogWarning("[ERROR-HANDLER] Process not found. ProcessId={ProcessId}", notification.ProcessId);
//                 return;
//             }
//
//             var token = await _uow.Tokens.GetByIdAsync(notification.TokenId, txCt);
//             if (token == null)
//             {
//                 _logger.LogWarning("[ERROR-HANDLER] Token not found. TokenId={TokenId}", notification.TokenId);
//                 return;
//             }
//
//             // ✅ Step 1: Lookup error subscriptions by ErrorCode (matching specific code or "Any" = null)
//             // Note: For now, we look up all error subscriptions for the process.
//             // In BPMN, boundary events can catch errors from child elements, so we search broadly.
//             var errorSubscriptions = await _uow.BoundarySubscriptions.GetActiveErrorSubscriptionsByErrorCodeAsync(
//                 notification.ProcessId,
//                 notification.ErrorCode,
//                 txCt);
//
//             var subscriptionsList = errorSubscriptions.ToList();
//             _logger.LogInformation(
//                 "[ERROR-HANDLER] Found {Count} error subscriptions for ErrorCode={ErrorCode}. ProcessId={ProcessId} ElementId={ElementId}",
//                 subscriptionsList.Count,
//                 notification.ErrorCode,
//                 notification.ProcessId,
//                 notification.ElementId);
//
//             // Debug: Log all found subscriptions
//             foreach (var sub in subscriptionsList)
//             {
//                 _logger.LogDebug(
//                     "[ERROR-HANDLER] Found subscription: Id={Id} TokenId={TokenId} ElementId={ElementId} ErrorCode={ErrorCode} State={State}",
//                     sub.Id,
//                     sub.TokenId,
//                     sub.AttachedToElementId,
//                     sub.ErrorCode,
//                     sub.State);
//             }
//
//             if (subscriptionsList.Count == 0)
//             {
//                 _logger.LogWarning(
//                     "[ERROR-HANDLER] No error subscriptions found. Propagating to parent scopes or marking as unhandled. ProcessId={ProcessId} ErrorCode={ErrorCode}",
//                     notification.ProcessId,
//                     notification.ErrorCode);
//                 
//                 // No handler found - mark as unhandled (will be handled by orchestrator)
//                 await HandleUnhandledErrorAsync(process, token, notification, txCt);
//                 return;
//             }
//
//             // ✅ Step 2: Find matching subscription with scope-aware lookup
//             // Priority: same ScopeId first, then parent scopes, then any match
//             BoundaryEventSubscription? matchedSubscription = null;
//             Guid? matchedScopeId = null;
//
//             // Get all tokens to build scope hierarchy (reuse if already loaded)
//             var processTokens = await _uow.Tokens.GetByProcessIdAsync(notification.ProcessId, txCt);
//             var tokenScopeMap = processTokens
//                 .Where(t => t.ScopeId.HasValue)
//                 .ToDictionary(t => t.Id, t => t.ScopeId!.Value);
//
//             // Build scope hierarchy: collect all ScopeIds from current scope up to root
//             var scopeHierarchy = new List<Guid>();
//             if (notification.ScopeId.HasValue)
//             {
//                 scopeHierarchy.Add(notification.ScopeId.Value);
//                 
//                 // Find parent scopes by following ParentTokenIds
//                 var currentToken = token;
//                 var visitedScopes = new HashSet<Guid> { notification.ScopeId.Value };
//                 
//                 while (currentToken != null && currentToken.ParentTokenIds.Any())
//                 {
//                     var parentToken = processTokens.FirstOrDefault(t => currentToken.ParentTokenIds.Contains(t.Id));
//                     if (parentToken == null) break;
//                     
//                     if (parentToken.ScopeId.HasValue && !visitedScopes.Contains(parentToken.ScopeId.Value))
//                     {
//                         scopeHierarchy.Add(parentToken.ScopeId.Value);
//                         visitedScopes.Add(parentToken.ScopeId.Value);
//                     }
//                     
//                     currentToken = parentToken;
//                 }
//             }
//
//             _logger.LogDebug(
//                 "[ERROR-HANDLER] Scope hierarchy for error lookup. ScopeHierarchy={Hierarchy}",
//                 string.Join(" -> ", scopeHierarchy));
//
//             // Find subscription in scope hierarchy (same scope first, then parent scopes)
//             foreach (var scopeId in scopeHierarchy)
//             {
//                 matchedSubscription = subscriptionsList
//                     .Where(s => tokenScopeMap.TryGetValue(s.TokenId, out var tokenScopeId) 
//                              && tokenScopeId == scopeId)
//                     .FirstOrDefault();
//
//                 if (matchedSubscription != null)
//                 {
//                     matchedScopeId = scopeId;
//                     _logger.LogInformation(
//                         "[ERROR-HANDLER] ✅ Found matching subscription in scope. SubscriptionId={SubscriptionId} ScopeId={ScopeId}",
//                         matchedSubscription.Id,
//                         matchedScopeId);
//                     break;
//                 }
//             }
//
//             // If no match in scope hierarchy, try to find any matching subscription (fallback)
//             if (matchedSubscription == null)
//             {
//                 matchedSubscription = subscriptionsList.FirstOrDefault();
//                 if (matchedSubscription != null)
//                 {
//                     // Get ScopeId from the token that owns this subscription
//                     var subscriptionToken = await _uow.Tokens.GetByIdAsync(matchedSubscription.TokenId, txCt);
//                     matchedScopeId = subscriptionToken?.ScopeId;
//                     _logger.LogInformation(
//                         "[ERROR-HANDLER] ✅ Found matching subscription (fallback - no scope match). SubscriptionId={SubscriptionId} ScopeId={ScopeId}",
//                         matchedSubscription.Id,
//                         matchedScopeId);
//                 }
//             }
//
//             if (matchedSubscription == null)
//             {
//                 _logger.LogWarning(
//                     "[ERROR-HANDLER] No matching subscription found after filtering. Marking as unhandled. ProcessId={ProcessId} ErrorCode={ErrorCode}",
//                     notification.ProcessId,
//                     notification.ErrorCode);
//                 
//                 await HandleUnhandledErrorAsync(process, token, notification, txCt);
//                 return;
//             }
//
//             // ✅ Step 3: Execute boundary event (dispatch handler flow) while subscription is still Active
//             _logger.LogInformation(
//                 "[ERROR-HANDLER] Executing boundary event. SubscriptionId={SubscriptionId} BoundaryEventId={BoundaryEventId}",
//                 matchedSubscription.Id,
//                 matchedSubscription.BoundaryEventId);
//
//             await _boundaryEventExecutor.ExecuteAsync(matchedSubscription.Id, txCt);
//
//             // ✅ Step 4: Cancel other subscriptions in the same scope
//             // Note: Subscription is already marked as triggered by ExecuteAsync
//
//             _logger.LogInformation(
//                 "[ERROR-HANDLER] ✅ Boundary event executed. SubscriptionId={SubscriptionId} ScopeId={ScopeId}",
//                 matchedSubscription.Id,
//                 matchedScopeId);
//
//             // Cancel other subscriptions in the same scope
//             if (matchedScopeId.HasValue)
//             {
//                 var scopeTokenIds = processTokens
//                     .Where(t => t.ScopeId == matchedScopeId.Value)
//                     .Select(t => t.Id)
//                     .ToList();
//
//                 var otherSubscriptions = subscriptionsList
//                     .Where(s => s.Id != matchedSubscription.Id
//                              && scopeTokenIds.Contains(s.TokenId)
//                              && s.State == SubscriptionState.Active)
//                     .ToList();
//
//                 foreach (var sub in otherSubscriptions)
//                 {
//                     sub.Cancel();
//                     await _uow.BoundarySubscriptions.UpdateAsync(sub, txCt);
//
//                     // Cancel external job if exists
//                     if (!string.IsNullOrWhiteSpace(sub.ExternalJobKey))
//                     {
//                         try
//                         {
//                             await _timerScheduler.CancelAsync(sub.ExternalJobKey, txCt);
//                         }
//                         catch (Exception ex)
//                         {
//                             _logger.LogWarning(
//                                 ex,
//                                 "[ERROR-HANDLER] Failed to cancel external job. SubscriptionId={SubscriptionId}",
//                                 sub.Id);
//                         }
//                     }
//                 }
//
//                 _logger.LogInformation(
//                     "[ERROR-HANDLER] Canceled {Count} other subscriptions in scope. ScopeId={ScopeId}",
//                     otherSubscriptions.Count,
//                     matchedScopeId);
//             }
//
//             _logger.LogInformation(
//                 "[ERROR-HANDLER] ✅ Error boundary executed. SubscriptionId={SubscriptionId} ProcessId={ProcessId} TokenId={TokenId}",
//                 matchedSubscription.Id,
//                 notification.ProcessId,
//                 notification.TokenId);
//         }, ct);
//     }
//
//     /// <summary>
//     /// Handle unhandled error: convert all tokens to trace tokens and fail process
//     /// This is called when no matching subscription is found for the error.
//     /// </summary>
//     private System.Threading.Tasks.Task HandleUnhandledErrorAsync(
//         ProcessEntity process,
//         Token token,
//         ErrorRaisedEvent notification,
//         CancellationToken ct)
//     {
//         // Note: Actual unhandled error handling (converting tokens to trace, failing process)
//         // is done by ITokenProcessingOrchestrator.HandleBpmnErrorAsync after checking if error was handled.
//         // This method just logs that no subscription was found.
//         _logger.LogWarning(
//             "[ERROR-HANDLER] Unhandled error - no matching subscription found. ProcessId={ProcessId} TokenId={TokenId} ErrorCode={ErrorCode} ScopeId={ScopeId}",
//             notification.ProcessId,
//             notification.TokenId,
//             notification.ErrorCode,
//             notification.ScopeId);
//         
//         return System.Threading.Tasks.Task.CompletedTask;
//     }
//
//     /// <summary>
//     /// Create subscriptions برای boundary events attach شده به یک element
//     /// </summary>
//     private async System.Threading.Tasks.Task CreateSubscriptionsForElementAsync(
//         ProcessEntity process,
//         Token token,
//         string elementId,
//         BpmnRuntimeContext ctx,
//         CancellationToken ct)
//     {
//         // ✅ Token-Centric Model: Trace tokens never create subscriptions
//         if (!token.IsExecutable)
//         {
//             _logger.LogDebug(
//                 "[BOUNDARY-SUBSCRIPTION] Trace token => skipping subscription creation. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId}",
//                 process.Id,
//                 token.Id,
//                 elementId);
//             return;
//         }
//
//         _logger.LogDebug(
//             "[BOUNDARY-SUBSCRIPTION] Creating subscriptions for element. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId} BpmnProcessId={BpmnProcessId}",
//             process.Id,
//             token.Id,
//             elementId,
//             ctx.BpmnProcessId);
//
//         // Get boundary events attached to this element
//         var boundaryEvents = ctx.Model.GetBoundaryEvents(ctx.BpmnProcessId, elementId);
//         
//         _logger.LogDebug(
//             "[BOUNDARY-SUBSCRIPTION] GetBoundaryEvents returned {Count} boundary events. ElementId={ElementId} BpmnProcessId={BpmnProcessId}",
//             boundaryEvents.Count,
//             elementId,
//             ctx.BpmnProcessId);
//
//         if (boundaryEvents.Count == 0)
//         {
//             _logger.LogDebug(
//                 "[BOUNDARY-SUBSCRIPTION] No boundary events found for element. ElementId={ElementId} BpmnProcessId={BpmnProcessId}",
//                 elementId,
//                 ctx.BpmnProcessId);
//             return;
//         }
//
//         _logger.LogDebug(
//             "[BOUNDARY-SUBSCRIPTION] Creating subscriptions for element. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId} BoundaryEventsCount={Count}",
//             process.Id,
//             token.Id,
//             elementId,
//             boundaryEvents.Count);
//
//         foreach (var boundaryEvent in boundaryEvents)
//         {
//             var kind = DetermineBoundaryKind(boundaryEvent);
//             if (kind == null)
//             {
//                 _logger.LogWarning(
//                     "[BOUNDARY-SUBSCRIPTION] Unknown boundary event type. BoundaryEventId={BoundaryEventId} ElementId={ElementId}",
//                     boundaryEvent.id,
//                     elementId);
//                 continue;
//             }
//
//             var isInterrupting = boundaryEvent.cancelActivity; // Default is true (interrupting) per BPMN spec
//             DateTimeOffset? dueAt = null;
//             string? correlationKey = null;
//             string? errorCode = null;
//
//             // Extract event-specific data
//             if (kind == BoundaryKind.Timer)
//             {
//                 dueAt = ExtractTimerDueAt(boundaryEvent, _clock);
//                 if (dueAt == null)
//                 {
//                     _logger.LogWarning(
//                         "[BOUNDARY-SUBSCRIPTION] Timer boundary event has no valid timer definition. BoundaryEventId={BoundaryEventId}",
//                         boundaryEvent.id);
//                     continue;
//                 }
//             }
//             else if (kind == BoundaryKind.Message)
//             {
//                 correlationKey = ExtractMessageCorrelationKey(boundaryEvent);
//             }
//             else if (kind == BoundaryKind.Error)
//             {
//                 errorCode = ExtractErrorCode(boundaryEvent, ctx);
//             }
//
//             // Check if this element is an activity that creates a new scope (UserTask, SubProcess, etc.)
//             // If so, set ActivityInstanceId for this token
//             var element = ctx.Model.GetElementById(ctx.BpmnProcessId, elementId);
//             var isActivity = element is BpmnActivity;
//             
//             if (isActivity && !token.ActivityInstanceId.HasValue)
//             {
//                 // Create new ActivityInstanceId for this activity
//                 var activityInstanceId = Guid.NewGuid();
//                 token.SetActivityInstance(activityInstanceId);
//                 _logger.LogDebug(
//                     "[BOUNDARY-SUBSCRIPTION] Set ActivityInstanceId for token. TokenId={TokenId} ElementId={ElementId} ActivityInstanceId={ActivityInstanceId}",
//                     token.Id,
//                     elementId,
//                     activityInstanceId);
//             }
//
//             var subscription = new BoundaryEventSubscription(
//                 process.Id,
//                 token.Id,
//                 elementId,
//                 boundaryEvent.id!,
//                 kind.Value,
//                 isInterrupting,
//                 dueAt,
//                 correlationKey,
//                 errorCode,
//                 token.ActivityInstanceId,
//                 token.ScopeId);
//
//             await _uow.BoundarySubscriptions.AddAsync(subscription, ct);
//             _logger.LogDebug(
//                 "[BOUNDARY-SUBSCRIPTION] Subscription saved. SubscriptionId={SubscriptionId} Kind={Kind}",
//                 subscription.Id,
//                 kind);
//
//             // ⚠️ IMPORTANT: Timer scheduling باید بعد از commit انجام شود (Outbox pattern)
//             // فعلاً داخل transaction انجام می‌شود - در production باید به domain event تبدیل شود
//             // TODO: Create BoundaryTimerSubscriptionCreatedEvent و handler که بعد از commit schedule می‌کند
//             if (kind == BoundaryKind.Timer && dueAt.HasValue)
//             {
//                 try
//                 {
//                     var jobKey = await _timerScheduler.ScheduleAsync(subscription.Id, dueAt.Value, ct);
//                     subscription.SetExternalJobKey(jobKey);
//                     await _uow.BoundarySubscriptions.UpdateAsync(subscription, ct);
//                 }
//                 catch (Exception ex)
//                 {
//                     _logger.LogError(
//                         ex,
//                         "[BOUNDARY-SUBSCRIPTION] Failed to schedule timer. SubscriptionId={SubscriptionId} DueAt={DueAt}",
//                         subscription.Id,
//                         dueAt);
//                     // Continue - subscription created but timer not scheduled
//                 }
//             }
//
//             _logger.LogInformation(
//                 "[BOUNDARY-SUBSCRIPTION] Subscription created. SubscriptionId={SubscriptionId} TokenId={TokenId} AttachedToElementId={ElementId} BoundaryEventId={BoundaryEventId} Kind={Kind} ErrorCode={ErrorCode} IsInterrupting={IsInterrupting}",
//                 subscription.Id,
//                 token.Id,
//                 elementId,
//                 boundaryEvent.id,
//                 kind,
//                 errorCode,
//                 isInterrupting);
//         }
//     }
//
//     /// <summary>
//     /// Cancel subscriptions برای یک element (وقتی token از آن خارج می‌شود)
//     /// </summary>
//     private async System.Threading.Tasks.Task CancelSubscriptionsForElementAsync(
//         Guid processId,
//         Guid tokenId,
//         string elementId,
//         CancellationToken ct)
//     {
//         _logger.LogDebug(
//             "[BOUNDARY-SUBSCRIPTION] Canceling subscriptions for element. ElementId={ElementId} TokenId={TokenId} ProcessId={ProcessId}",
//             elementId,
//             tokenId,
//             processId);
//
//         var subscriptions = await _uow.BoundarySubscriptions.GetActiveByAttachedElementAsync(
//             processId,
//             elementId,
//             ct);
//
//         _logger.LogDebug(
//             "[BOUNDARY-SUBSCRIPTION] Found {Count} active subscriptions to cancel. ElementId={ElementId}",
//             subscriptions.Count(),
//             elementId);
//
//         var tokenSubscriptions = subscriptions.Where(s => s.TokenId == tokenId).ToList();
//         if (tokenSubscriptions.Count == 0)
//             return;
//
//         _logger.LogDebug(
//             "[BOUNDARY-SUBSCRIPTION] Canceling subscriptions for element. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId} Count={Count}",
//             processId,
//             tokenId,
//             elementId,
//             tokenSubscriptions.Count);
//
//         foreach (var subscription in tokenSubscriptions)
//         {
//             subscription.Cancel();
//
//             // Cancel external job if exists
//             if (!string.IsNullOrWhiteSpace(subscription.ExternalJobKey))
//             {
//                 try
//                 {
//                     await _timerScheduler.CancelAsync(subscription.ExternalJobKey, ct);
//                 }
//                 catch (Exception ex)
//                 {
//                     _logger.LogWarning(
//                         ex,
//                         "[BOUNDARY-SUBSCRIPTION] Failed to cancel external job. SubscriptionId={SubscriptionId} JobKey={JobKey}",
//                         subscription.Id,
//                         subscription.ExternalJobKey);
//                 }
//             }
//
//             await _uow.BoundarySubscriptions.UpdateAsync(subscription, ct);
//         }
//     }
//
//     /// <summary>
//     /// Cancel همه subscription‌های یک token
//     /// </summary>
//     private async System.Threading.Tasks.Task CancelAllSubscriptionsForTokenAsync(Guid tokenId, CancellationToken ct)
//     {
//         var subscriptions = await _uow.BoundarySubscriptions.GetByTokenIdAsync(tokenId, ct);
//         var activeSubscriptions = subscriptions.Where(s => s.State == SubscriptionState.Active).ToList();
//
//         if (activeSubscriptions.Count == 0)
//             return;
//
//         _logger.LogDebug(
//             "[BOUNDARY-SUBSCRIPTION] Canceling all subscriptions for token. TokenId={TokenId} Count={Count}",
//             tokenId,
//             activeSubscriptions.Count);
//
//         foreach (var subscription in activeSubscriptions)
//         {
//             subscription.Cancel();
//
//             if (!string.IsNullOrWhiteSpace(subscription.ExternalJobKey))
//             {
//                 try
//                 {
//                     await _timerScheduler.CancelAsync(subscription.ExternalJobKey, ct);
//                 }
//                 catch (Exception ex)
//                 {
//                     _logger.LogWarning(
//                         ex,
//                         "[BOUNDARY-SUBSCRIPTION] Failed to cancel external job. SubscriptionId={SubscriptionId} JobKey={JobKey}",
//                         subscription.Id,
//                         subscription.ExternalJobKey);
//                 }
//             }
//
//             await _uow.BoundarySubscriptions.UpdateAsync(subscription, ct);
//         }
//     }
//
//     private static BoundaryKind? DetermineBoundaryKind(BpmnBoundaryEvent boundaryEvent)
//     {
//         if (boundaryEvent.Items == null || boundaryEvent.Items.Count() == 0)
//             return null;
//
//         var firstItem = boundaryEvent.Items[0];
//         return firstItem switch
//         {
//             BpmnTimerEventDefinition => BoundaryKind.Timer,
//             BpmnMessageEventDefinition => BoundaryKind.Message,
//             BpmnSignalEventDefinition => BoundaryKind.Signal,
//             BpmnErrorEventDefinition => BoundaryKind.Error,
//             BpmnEscalationEventDefinition => BoundaryKind.Escalation,
//             BpmnConditionalEventDefinition => BoundaryKind.Conditional,
//             BpmnCancelEventDefinition => BoundaryKind.Cancel,
//             BpmnCompensateEventDefinition => BoundaryKind.Compensation,
//             _ => null
//         };
//     }
//
//     private static DateTimeOffset? ExtractTimerDueAt(BpmnBoundaryEvent boundaryEvent, IClock clock)
//     {
//         if (boundaryEvent.Items == null)
//             return null;
//
//         var timerDef = boundaryEvent.Items.OfType<BpmnTimerEventDefinition>().FirstOrDefault();
//         if (timerDef == null)
//             return null;
//
//         // TODO: Parse timer expression (timeDuration, timeDate, timeCycle)
//         // فعلاً فقط timeDuration با ثانیه ساده را support می‌کنیم
//         if (timerDef.TimeDuration?.Text != null && timerDef.TimeDuration.Text.Length > 0)
//         {
//             var durationStr = timerDef.TimeDuration.Text[0];
//             if (TryParseDuration(durationStr, out var duration))
//             {
//                 return clock.Now.Add(duration);
//             }
//         }
//
//         return null;
//     }
//
//     private static bool TryParseDuration(string durationStr, out TimeSpan duration)
//     {
//         duration = TimeSpan.Zero;
//         
//         // Simple parsing: "PT5S" = 5 seconds, "PT1M" = 1 minute, "PT1H" = 1 hour
//         // TODO: Full ISO 8601 duration parsing
//         if (durationStr.StartsWith("PT", StringComparison.OrdinalIgnoreCase))
//         {
//             var numberStr = durationStr.Substring(2, durationStr.Length - 3);
//             var unit = durationStr[durationStr.Length - 1];
//             
//             if (int.TryParse(numberStr, out var number))
//             {
//                 duration = unit switch
//                 {
//                     'S' => TimeSpan.FromSeconds(number),
//                     'M' => TimeSpan.FromMinutes(number),
//                     'H' => TimeSpan.FromHours(number),
//                     _ => TimeSpan.Zero
//                 };
//                 return duration != TimeSpan.Zero;
//             }
//         }
//
//         return false;
//     }
//
//     private static string? ExtractMessageCorrelationKey(BpmnBoundaryEvent boundaryEvent)
//     {
//         if (boundaryEvent.Items == null)
//             return null;
//
//         var messageDef = boundaryEvent.Items.OfType<BpmnMessageEventDefinition>().FirstOrDefault();
//         return messageDef?.messageRef?.Name;
//     }
//
//     /// <summary>
//     /// Extract error code from boundary event's error definition.
//     /// Returns null if error element has no errorCode (catches all errors - "Any" state).
//     /// </summary>
//     private string? ExtractErrorCode(BpmnBoundaryEvent boundaryEvent, BpmnRuntimeContext ctx)
//     {
//         if (boundaryEvent.Items == null)
//             return null;
//
//         var errorDef = boundaryEvent.Items.OfType<BpmnErrorEventDefinition>().FirstOrDefault();
//         if (errorDef == null)
//             return null;
//
//         // If errorRef is null or empty, it catches all errors ("Any")
//         if (string.IsNullOrWhiteSpace(errorDef.errorRef?.Name))
//             return null; // null = catches all errors
//
//         // Get error element to extract actual errorCode
//         var errorElementId = errorDef.errorRef.Name;
//         try
//         {
//             var errorElement = ctx.Model.GetErrorElement(errorElementId);
//             if (errorElement == null)
//             {
//                 _logger.LogWarning(
//                     "[BOUNDARY-SUBSCRIPTION] Error element not found. ErrorElementId={ErrorElementId} BoundaryEventId={BoundaryEventId}",
//                     errorElementId,
//                     boundaryEvent.id);
//                 return null; // Fallback: treat as "Any" if element not found
//             }
//
//             // Return errorCode from error element (null if empty = catches all)
//             return string.IsNullOrWhiteSpace(errorElement.errorCode) ? null : errorElement.errorCode;
//         }
//         catch (Exception ex)
//         {
//             _logger.LogWarning(
//                 ex,
//                 "[BOUNDARY-SUBSCRIPTION] Exception while extracting error code. ErrorElementId={ErrorElementId} BoundaryEventId={BoundaryEventId}",
//                 errorElementId,
//                 boundaryEvent.id);
//             return null; // Fallback: treat as "Any"
//         }
//     }
// }
