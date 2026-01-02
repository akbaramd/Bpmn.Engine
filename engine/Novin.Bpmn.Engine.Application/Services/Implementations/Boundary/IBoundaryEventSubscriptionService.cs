using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public interface IBoundaryEventSubscriptionService
{
    Task<IReadOnlyList<BoundaryEventSubscription>> SubscribeBoundaryEventsAsync(NodeInstance node, CancellationToken ct);
}

public sealed class BoundaryEventSubscriptionService : IBoundaryEventSubscriptionService
{
    private readonly IProcessRepository _processRepository;
    private readonly ITimerScheduler _timerScheduler;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IBoundarySubscriptionRepository _boundarySubscriptionRepository;
    private readonly IBpmnQuery _bpmnQuery;
    private readonly ILogger<BoundaryEventSubscriptionService> _logger;

    public BoundaryEventSubscriptionService(
        IProcessRepository processRepository,
        ITimerScheduler timerScheduler,
        IDeploymentRepository deploymentRepository,
        IBoundarySubscriptionRepository boundarySubscriptionRepository,
        IBpmnQuery bpmnQuery,
        ILogger<BoundaryEventSubscriptionService> logger)
    {
        _processRepository = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
        _timerScheduler = timerScheduler ?? throw new ArgumentNullException(nameof(timerScheduler));
        _deploymentRepository = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
        _boundarySubscriptionRepository = boundarySubscriptionRepository ?? throw new ArgumentNullException(nameof(boundarySubscriptionRepository));
        _bpmnQuery = bpmnQuery ?? throw new ArgumentNullException(nameof(bpmnQuery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<BoundaryEventSubscription>> SubscribeBoundaryEventsAsync(NodeInstance node, CancellationToken ct)
    {
        _logger.LogInformation(
            "[BOUNDARY_SUBSCRIBE] Starting boundary event subscription. NodeId={NodeId} ElementId={ElementId} ProcessId={ProcessId}",
            node?.Id, node?.ElementId, node?.ProcessId);

        if (node is null) throw new ArgumentNullException(nameof(node));
        if (string.IsNullOrWhiteSpace(node.ElementId))
        {
            _logger.LogWarning("[BOUNDARY_SUBSCRIBE] Node has no ElementId. NodeId={NodeId}", node.Id);
            return Array.Empty<BoundaryEventSubscription>();
        }

        // 1) Load process + deployment
        var process = await _processRepository.GetByIdAsync(node.ProcessId);
        if (process is null)
        {
            _logger.LogWarning("[BOUNDARY_SUBSCRIBE] Process not found. ProcessId={ProcessId}", node.ProcessId);
            return Array.Empty<BoundaryEventSubscription>();
        }

        var deployment = await _deploymentRepository.GetByIdAsync(process.DeploymentId);
        if (deployment is null)
        {
            _logger.LogWarning("[BOUNDARY_SUBSCRIBE] Deployment not found. DeploymentId={DeploymentId}", process.DeploymentId);
            return Array.Empty<BoundaryEventSubscription>();
        }

        _logger.LogDebug(
            "[BOUNDARY_SUBSCRIBE] Loaded process and deployment. ProcessId={ProcessId} ProcessBpmnId={ProcessBpmnId} DeploymentId={DeploymentId}",
            process.Id, process.ProcessBpmnId, deployment.Id);

        // 2) Load boundary events from BPMN model
        var boundaryEvents = _bpmnQuery.GetAllElementsOfType<BpmnBoundaryEvent>(deployment, process.ProcessBpmnId);
        if (boundaryEvents is null || boundaryEvents.Count == 0)
        {
            _logger.LogInformation(
                "[BOUNDARY_SUBSCRIBE] No boundary events found in BPMN model. ProcessBpmnId={ProcessBpmnId}",
                process.ProcessBpmnId);
            return Array.Empty<BoundaryEventSubscription>();
        }

        _logger.LogInformation(
            "[BOUNDARY_SUBSCRIBE] Found {Count} boundary events in BPMN model. ProcessBpmnId={ProcessBpmnId} LookingForElementId={ElementId}",
            boundaryEvents.Count, process.ProcessBpmnId, node.ElementId);

        var created = new List<BoundaryEventSubscription>();

        foreach (var boundaryEvent in boundaryEvents)
        {
            
            ct.ThrowIfCancellationRequested();

            try
            {
                // Validate boundary event
                if (boundaryEvent is null)
                {
                    _logger.LogWarning("[BOUNDARY_SUBSCRIBE] Skipping null boundary event");
                    continue;
                }

                var boundaryElementId = ReadString(boundaryEvent, "id");
                if (string.IsNullOrWhiteSpace(boundaryElementId))
                {
                    _logger.LogWarning("[BOUNDARY_SUBSCRIBE] Skipping boundary event - no ID found");
                    continue;
                }

                _logger.LogDebug(
                    "[BOUNDARY_SUBSCRIBE] Processing boundary event. BoundaryId={BoundaryId} NodeElementId={NodeElementId} BoundaryEventType={BoundaryEventType}",
                    boundaryElementId, node.ElementId, boundaryEvent.GetType().Name);

            // Debug: Log all properties of boundary event
            var boundaryProps = boundaryEvent.GetType().GetProperties(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public);
            var propNames = string.Join(", ", boundaryProps.Select(p => p.Name));
            _logger.LogDebug(
                "[BOUNDARY_SUBSCRIBE] Boundary event properties. BoundaryId={BoundaryId} Properties={Properties}",
                boundaryElementId, propNames);

            // 3) Match attachedToRef to node.ElementId
            // attachedToRef can be a string or an object with Name property
            var attachedToRefObj = ReadObject(boundaryEvent, "attachedToRef");
            var attachedToId = ReadString(boundaryEvent, "attachedToRef")
                               ?? (attachedToRefObj != null ? ReadString(attachedToRefObj, "Name") : null)
                               ?? (attachedToRefObj != null ? ReadString(attachedToRefObj, "name") : null)
                               ?? (attachedToRefObj != null ? ReadString(attachedToRefObj, "id") : null);

            _logger.LogDebug(
                "[BOUNDARY_SUBSCRIBE] Boundary event attachedToRef extraction. BoundaryId={BoundaryId} AttachedToRefObjType={AttachedToRefObjType} AttachedToId={AttachedToId}",
                boundaryElementId, attachedToRefObj?.GetType().Name ?? "NULL", attachedToId ?? "NULL");

            _logger.LogDebug(
                "[BOUNDARY_SUBSCRIBE] Boundary event attachedToRef. BoundaryId={BoundaryId} AttachedToRef={AttachedToRef} NodeElementId={NodeElementId}",
                boundaryElementId, attachedToId ?? "NULL", node.ElementId);

            if (string.IsNullOrWhiteSpace(attachedToId))
            {
                _logger.LogDebug(
                    "[BOUNDARY_SUBSCRIBE] Skipping boundary event - no attachedToRef. BoundaryId={BoundaryId}",
                    boundaryElementId);
                continue;
            }

            if (!string.Equals(attachedToId, node.ElementId, StringComparison.Ordinal))
            {
                _logger.LogDebug(
                    "[BOUNDARY_SUBSCRIBE] Skipping boundary event - attachedToRef mismatch. BoundaryId={BoundaryId} AttachedToRef={AttachedToRef} NodeElementId={NodeElementId}",
                    boundaryElementId, attachedToId, node.ElementId);
                continue;
            }

            _logger.LogInformation(
                "[BOUNDARY_SUBSCRIBE] Boundary event matches node. BoundaryId={BoundaryId} AttachedToRef={AttachedToRef} NodeElementId={NodeElementId}",
                boundaryElementId, attachedToId, node.ElementId);

                // 4) Detect boundary kind + extract definition (Timer/Error/Message)
                if (!TryExtractBoundaryDefinition(boundaryEvent,
                        out var kind,
                        out var timerType,
                        out var timerExpr,
                        out var errorCode,
                        out var messageName))
                {
                    _logger.LogWarning(
                        "[BOUNDARY_SUBSCRIBE] Failed to extract boundary definition. BoundaryId={BoundaryId} AttachedToRef={AttachedToRef}",
                        boundaryElementId, attachedToId);
                    continue;
                }

              

                _logger.LogInformation(
                    "[BOUNDARY_SUBSCRIBE] Boundary event type detected. BoundaryId={BoundaryId} Kind={Kind} TimerType={TimerType} TimerExpr={TimerExpr} ErrorCode={ErrorCode} MessageName={MessageName}",
                    boundaryElementId, kind, timerType?.ToString() ?? "NULL", timerExpr ?? "NULL", errorCode ?? "NULL", messageName ?? "NULL");

            // 5) Idempotency: avoid duplicates for same node-instance + boundary element while Active
            if (string.IsNullOrWhiteSpace(boundaryElementId))
            {
                _logger.LogWarning(
                    "[BOUNDARY_SUBSCRIBE] Skipping boundary event - no element ID. AttachedToRef={AttachedToRef}",
                    attachedToId);
                continue;
            }

            var exists = await _boundarySubscriptionRepository.ExistsActiveAsync(
                processId: process.Id,
                nodeInstanceId: node.Id,
                boundaryElementId: boundaryElementId,
                ct: ct);

            if (exists)
            {
                _logger.LogDebug(
                    "[BOUNDARY_SUBSCRIBE] Skipping boundary event - already exists. BoundaryId={BoundaryId} NodeInstanceId={NodeInstanceId}",
                    boundaryElementId, node.Id);
                continue;
            }

            // 6) Timer compute DueAt (required for Quartz scheduling)
            DateTimeOffset? dueAt = null;
            DateTimeOffset? nextDueAtUtc = null;

            if (kind == BoundaryKind.Timer)
            {
                if (timerType is null || string.IsNullOrWhiteSpace(timerExpr))
                {
                    _logger.LogWarning(
                        "[BOUNDARY_SUBSCRIBE] Timer boundary missing type or expression. BoundaryId={BoundaryId} TimerType={TimerType} TimerExpr={TimerExpr}",
                        boundaryElementId, timerType?.ToString() ?? "NULL", timerExpr ?? "NULL");
                    continue;
                }

                var startedAtUtc = TryGetNodeStartedAtUtc(node);

                _logger.LogDebug(
                    "[BOUNDARY_SUBSCRIBE] Computing timer DueAt. BoundaryId={BoundaryId} TimerType={TimerType} TimerExpr={TimerExpr} StartedAtUtc={StartedAtUtc}",
                    boundaryElementId, timerType, timerExpr, startedAtUtc?.ToString("O") ?? "NULL");

                // timeDuration/timeCycle نیازمند anchor time هستند
                if (timerType is TimerType.TimeDuration or TimerType.TimeCycle)
                {
                    if (startedAtUtc is null)
                    {
                        _logger.LogWarning(
                            "[BOUNDARY_SUBSCRIBE] Timer boundary requires node.StartedAtUtc (or equivalent). NodeId={NodeId} ElementId={ElementId} BoundaryId={BoundaryId} TimerType={TimerType}",
                            node.Id, node.ElementId, boundaryElementId, timerType);
                        continue;
                    }
                }

                dueAt = ComputeDueAt(timerType.Value, timerExpr!, startedAtUtc);
                if (!dueAt.HasValue)
                {
                    _logger.LogWarning(
                        "[BOUNDARY_SUBSCRIBE] Failed to compute DueAt for timer boundary. NodeId={NodeId} ElementId={ElementId} BoundaryId={BoundaryId} TimerType={TimerType} Expr={Expr}",
                        node.Id, node.ElementId, boundaryElementId, timerType, timerExpr);
                    continue;
                }

                if (timerType == TimerType.TimeCycle)
                    nextDueAtUtc = dueAt;

                _logger.LogInformation(
                    "[BOUNDARY_SUBSCRIBE] Timer DueAt computed. BoundaryId={BoundaryId} TimerType={TimerType} DueAt={DueAt}",
                    boundaryElementId, timerType, dueAt.Value.ToString("O"));
            }

            // 7) interrupting flag (cancelActivity attribute)
            var cancelActivity = ReadBool(boundaryEvent, "cancelActivity") ?? false;

            _logger.LogInformation(
                "[BOUNDARY_SUBSCRIBE] Creating subscription. BoundaryId={BoundaryId} Kind={Kind} IsInterrupting={IsInterrupting} DueAt={DueAt}",
                boundaryElementId, kind, cancelActivity, dueAt?.ToString("O") ?? "NULL");


            // 8) Create subscription (پر کردن فیلدهای تایمر و غیره)
            var sub = BoundaryEventSubscription.Create(
                processId: process.Id,
                tokenId: node.TokenId,
                nodeInstanceId: node.Id,
                hostElementId: node.ElementId,
                boundaryElementId: boundaryElementId,
                kind: kind,
                isInterrupting: cancelActivity,
                dueAt: dueAt,
                correlationKey: messageName,
                errorCode: errorCode,
                activityInstanceId: node.ActivityInstanceId,
                tokenScopeId: TryGetNodeScopeId(node),
                meta: MetaBag.Empty,
                timerType: timerType,
                timerExpression: timerExpr,
                nextDueAtUtc: nextDueAtUtc
            );

            if (kind == BoundaryKind.Timer)
            {
                await _timerScheduler.ScheduleOnceAsync(sub.Id, dueAt.Value, ct);
            }

            // optional: ثبت source برای debugging
            // sub.SetMeta("source", "node-created-subscribe");

            await _boundarySubscriptionRepository.AddAsync(sub, ct);
            created.Add(sub);

                _logger.LogInformation(
                    "[BOUNDARY_SUBSCRIBE] Boundary subscription created successfully. SubscriptionId={SubscriptionId} Kind={Kind} Host={Host} Boundary={Boundary} NodeInstanceId={NodeInstanceId} IsInterrupting={IsInterrupting} DueAt={DueAt}",
                    sub.Id, kind, node.ElementId, boundaryElementId, node.Id, cancelActivity, dueAt?.ToString("O") ?? "NULL");
            }
            catch (Exception ex)
            {
                var boundaryElementId = ReadString(boundaryEvent, "id") ?? "UNKNOWN";
                _logger.LogError(ex,
                    "[BOUNDARY_SUBSCRIBE] Error processing boundary event. BoundaryId={BoundaryId} NodeElementId={NodeElementId} Error={Error}",
                    boundaryElementId, node.ElementId, ex.Message);
                // Continue processing other boundary events instead of failing completely
                continue;
            }
        }

        _logger.LogInformation(
            "[BOUNDARY_SUBSCRIBE] Completed boundary event subscription. NodeId={NodeId} ElementId={ElementId} CreatedCount={CreatedCount}",
            node.Id, node.ElementId, created.Count);

        return created;
    }

    // =========================================================
    // Boundary definition extraction
    // =========================================================

    private bool TryExtractBoundaryDefinition(
        BpmnBoundaryEvent e,
        out BoundaryKind kind,
        out TimerType? timerType,
        out string? timerExpr,
        out string? errorCode,
        out string? messageName)
    {
        kind = default;
        timerType = null;
        timerExpr = null;
        errorCode = null;
        messageName = null;

        try
        {
            // Validate input
            if (e is null)
            {
                _logger.LogWarning("[BOUNDARY_EXTRACT] Boundary event is null");
                return false;
            }

            var boundaryId = ReadString(e, "id") ?? "UNKNOWN";

            // Check Items first (event definitions are stored in Items array)
            if (e.Items != null && e.Items.Length > 0)
            {
                _logger.LogDebug(
                    "[BOUNDARY_EXTRACT] Boundary event has {Count} items. BoundaryId={BoundaryId} ItemTypes={ItemTypes}",
                    e.Items.Length, boundaryId, string.Join(", ", e.Items.Select(i => i?.GetType().Name ?? "NULL")));

                // ---- TIMER ---- (matches your XML: <timerEventDefinition><timeDuration>PT10S</timeDuration>)
                var timerDef = e.Items.OfType<BpmnTimerEventDefinition>().FirstOrDefault();
                if (timerDef is not null)
                {
                    _logger.LogDebug(
                        "[BOUNDARY_EXTRACT] Timer definition found in Items. BoundaryId={BoundaryId} TimerDefType={TimerDefType}",
                        boundaryId, timerDef.GetType().Name);

                    // Validate timer definition
                    if (timerDef.TimeDate == null && timerDef.TimeDuration == null && timerDef.TimeCycle == null)
                    {
                        _logger.LogWarning(
                            "[BOUNDARY_EXTRACT] Timer definition has no time expression. BoundaryId={BoundaryId}",
                            boundaryId);
                        return false;
                    }

                    // Read timer expressions from properties
                    var timeDate = timerDef.TimeDate?.Text?.FirstOrDefault()?.Trim();
                    var timeDuration = timerDef.TimeDuration?.Text?.FirstOrDefault()?.Trim();
                    var timeCycle = timerDef.TimeCycle?.Text?.FirstOrDefault()?.Trim();

                _logger.LogDebug(
                    "[BOUNDARY_EXTRACT] Timer expression values. BoundaryId={BoundaryId} TimeDate={TimeDate} TimeDuration={TimeDuration} TimeCycle={TimeCycle}",
                    boundaryId, timeDate ?? "NULL", timeDuration ?? "NULL", timeCycle ?? "NULL");

                if (!string.IsNullOrWhiteSpace(timeDate))
                {
                    kind = BoundaryKind.Timer;
                    timerType = TimerType.TimeDate;
                    timerExpr = timeDate.Trim();
                    _logger.LogInformation(
                        "[BOUNDARY_EXTRACT] Timer boundary detected (TimeDate). BoundaryId={BoundaryId} Expression={Expression}",
                        boundaryId, timerExpr);
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(timeDuration))
                {
                    kind = BoundaryKind.Timer;
                    timerType = TimerType.TimeDuration;
                    timerExpr = timeDuration.Trim();
                    _logger.LogInformation(
                        "[BOUNDARY_EXTRACT] Timer boundary detected (TimeDuration). BoundaryId={BoundaryId} Expression={Expression}",
                        boundaryId, timerExpr);
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(timeCycle))
                {
                    kind = BoundaryKind.Timer;
                    timerType = TimerType.TimeCycle;
                    timerExpr = timeCycle.Trim();
                    _logger.LogInformation(
                        "[BOUNDARY_EXTRACT] Timer boundary detected (TimeCycle). BoundaryId={BoundaryId} Expression={Expression}",
                        boundaryId, timerExpr);
                    return true;
                }

                _logger.LogWarning(
                    "[BOUNDARY_EXTRACT] Timer definition found but no valid expression. BoundaryId={BoundaryId}",
                    boundaryId);
                return false;
            }

                // ---- ERROR ---- (matches your XML: <errorEventDefinition .../>)
                var errDef = e.Items.OfType<BpmnErrorEventDefinition>().FirstOrDefault();
                if (errDef is not null)
                {
                    _logger.LogDebug(
                        "[BOUNDARY_EXTRACT] Error definition found in Items. BoundaryId={BoundaryId} ErrorDefType={ErrorDefType}",
                        boundaryId, errDef.GetType().Name);

                    // در XML تو errorRef نیامده، پس errorCode ممکن است null بماند (OK)
                    // errorRef.Name is the ID of the error element, not the errorCode
                    // We need to check if errorRef exists and get errorCode from error element
                    errorCode = errDef.errorRef?.Name; // This is the error element ID, not the errorCode itself
                    // Note: To get actual errorCode, we'd need to look up the error element
                    // For now, we'll use the errorRef.Name as identifier

                    kind = BoundaryKind.Error;
                    _logger.LogInformation(
                        "[BOUNDARY_EXTRACT] Error boundary detected. BoundaryId={BoundaryId} ErrorCode={ErrorCode}",
                        boundaryId, errorCode ?? "NULL");
                    return true;
                }

                // ---- MESSAGE ---- (اگر بعداً اضافه کردی)
                var msgDef = e.Items.OfType<BpmnMessageEventDefinition>().FirstOrDefault();
                if (msgDef is not null)
                {
                    _logger.LogDebug(
                        "[BOUNDARY_EXTRACT] Message definition found in Items. BoundaryId={BoundaryId} MessageDefType={MessageDefType}",
                        boundaryId, msgDef.GetType().Name);

                    // Validate message definition
                    if (msgDef.messageRef == null || string.IsNullOrWhiteSpace(msgDef.messageRef.Name))
                    {
                        _logger.LogWarning(
                            "[BOUNDARY_EXTRACT] Message definition has no messageRef. BoundaryId={BoundaryId}",
                            boundaryId);
                        return false;
                    }

                    // messageRef.Name is the message ID/name
                    messageName = msgDef.messageRef.Name;

                    kind = BoundaryKind.Message;
                    _logger.LogInformation(
                        "[BOUNDARY_EXTRACT] Message boundary detected. BoundaryId={BoundaryId} MessageName={MessageName}",
                        boundaryId, messageName);
                    return true;
                }
            }

        // Fallback: Try reflection-based approach if Items is null or empty
        _logger.LogDebug(
            "[BOUNDARY_EXTRACT] Items is null or empty, trying reflection-based approach. BoundaryId={BoundaryId}",
            boundaryId);

        // ---- TIMER (Fallback) ----
        var timerDefFallback = ReadObject(e, "timerEventDefinition") ?? ReadObject(e, "timerEventDefinitions");
        if (timerDefFallback is not null)
        {
            timerDefFallback = FirstOrSelf(timerDefFallback);
            if (timerDefFallback is not null)
            {
                var timeDate = ReadExpressionText(timerDefFallback, "timeDate");
                var timeDuration = ReadExpressionText(timerDefFallback, "timeDuration");
                var timeCycle = ReadExpressionText(timerDefFallback, "timeCycle");

                if (!string.IsNullOrWhiteSpace(timeDate))
                {
                    kind = BoundaryKind.Timer;
                    timerType = TimerType.TimeDate;
                    timerExpr = timeDate.Trim();
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(timeDuration))
                {
                    kind = BoundaryKind.Timer;
                    timerType = TimerType.TimeDuration;
                    timerExpr = timeDuration.Trim();
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(timeCycle))
                {
                    kind = BoundaryKind.Timer;
                    timerType = TimerType.TimeCycle;
                    timerExpr = timeCycle.Trim();
                    return true;
                }
            }
        }

        // ---- ERROR (Fallback) ----
        var errDefFallback = ReadObject(e, "errorEventDefinition") ?? ReadObject(e, "errorEventDefinitions");
        if (errDefFallback is not null)
        {
            errDefFallback = FirstOrSelf(errDefFallback);
            if (errDefFallback is not null)
            {
                errorCode = ReadString(errDefFallback, "errorCode")
                           ?? ReadString(ReadObject(errDefFallback, "errorRef") ?? new object(), "errorCode")
                           ?? ReadString(ReadObject(errDefFallback, "errorRef") ?? new object(), "id")
                           ?? ReadString(ReadObject(errDefFallback, "errorRef") ?? new object(), "Name")
                           ?? ReadString(errDefFallback, "id");
                kind = BoundaryKind.Error;
                return true;
            }
        }

        // ---- MESSAGE (Fallback) ----
        var msgDefFallback = ReadObject(e, "messageEventDefinition") ?? ReadObject(e, "messageEventDefinitions");
        if (msgDefFallback is not null)
        {
            msgDefFallback = FirstOrSelf(msgDefFallback);
            if (msgDefFallback is not null)
            {
                messageName = ReadString(msgDefFallback, "messageName")
                             ?? ReadString(ReadObject(msgDefFallback, "messageRef") ?? new object(), "name")
                             ?? ReadString(ReadObject(msgDefFallback, "messageRef") ?? new object(), "Name")
                             ?? ReadString(ReadObject(msgDefFallback, "messageRef") ?? new object(), "id");
                kind = BoundaryKind.Message;
                return true;
            }
        }

            _logger.LogWarning(
                "[BOUNDARY_EXTRACT] No valid event definition found. BoundaryId={BoundaryId} ItemsCount={ItemsCount}",
                boundaryId, e.Items?.Length ?? 0);
            return false;
        }
        catch (Exception ex)
        {
            var boundaryId = ReadString(e, "id") ?? "UNKNOWN";
            _logger.LogError(ex,
                "[BOUNDARY_EXTRACT] Error extracting boundary definition. BoundaryId={BoundaryId} Error={Error}",
                boundaryId, ex.Message);
            return false;
        }
    }

    private static object? FirstOrSelf(object obj)
    {
        if (obj is null) return null;
        if (obj is string) return obj;

        if (obj is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
                return item;
        }

        return obj;
    }

    private static string? ReadExpressionText(object timerDef, string prop)
    {
        // 1) direct string: timeDuration="PT10S"
        var s = ReadString(timerDef, prop);
        if (!string.IsNullOrWhiteSpace(s))
            return s;

        // 2) object wrapper: timeDuration -> { body/value/text/... }
        var obj = ReadObject(timerDef, prop);
        if (obj is null) return null;

        return ReadString(obj, "body")
               ?? ReadString(obj, "value")
               ?? ReadString(obj, "Value")
               ?? ReadString(obj, "text")
               ?? ReadString(obj, "Text")
               ?? ReadString(obj, "InnerText");
    }

    // =========================================================
    // Timer computation
    // =========================================================

    private static DateTimeOffset? ComputeDueAt(TimerType type, string expr, DateTime? nodeStartedAtUtc)
    {
        switch (type)
        {
            case TimerType.TimeDate:
                // literal ISO supported; if FEEL exists, evaluate before parse
                if (DateTimeOffset.TryParse(expr, out var dt))
                    return dt;
                return null;

            case TimerType.TimeDuration:
                if (nodeStartedAtUtc is null) return null;
                try
                {
                    var dur = System.Xml.XmlConvert.ToTimeSpan(expr); // PT10S -> 10 sec
                    return new DateTimeOffset(nodeStartedAtUtc.Value, TimeSpan.Zero).Add(dur);
                }
                catch
                {
                    return null;
                }

            case TimerType.TimeCycle:
                if (nodeStartedAtUtc is null) return null;
                return ParseCycleFirstFire(expr, nodeStartedAtUtc.Value);

            default:
                return null;
        }
    }

    private static DateTimeOffset? ParseCycleFirstFire(string expr, DateTime startedAtUtc)
    {
        // Supported:
        // PT5M
        // R/PT5M
        // R3/PT10M
        // R/2026-01-01T00:00:00Z/PT10M
        try
        {
            if (!expr.StartsWith("R", StringComparison.OrdinalIgnoreCase))
            {
                var intervalOnly = System.Xml.XmlConvert.ToTimeSpan(expr);
                return new DateTimeOffset(startedAtUtc, TimeSpan.Zero).Add(intervalOnly);
            }

            var parts = expr.Split('/');
            if (parts.Length == 2)
            {
                var interval = System.Xml.XmlConvert.ToTimeSpan(parts[1]);
                return new DateTimeOffset(startedAtUtc, TimeSpan.Zero).Add(interval);
            }

            if (parts.Length == 3)
            {
                var startPart = parts[1];
                var durPart = parts[2];

                // interval
                var interval = System.Xml.XmlConvert.ToTimeSpan(durPart);

                // explicit start?
                if (DateTimeOffset.TryParse(startPart, out var startAt))
                    return startAt;

                // fallback = activity start + interval
                return new DateTimeOffset(startedAtUtc, TimeSpan.Zero).Add(interval);
            }

            // fallback: last part as duration
            var last = parts.Last();
            var fallbackInterval = System.Xml.XmlConvert.ToTimeSpan(last);
            return new DateTimeOffset(startedAtUtc, TimeSpan.Zero).Add(fallbackInterval);
        }
        catch
        {
            return null;
        }
    }

    // =========================================================
    // NodeInstance helpers (supports nullable)
    // =========================================================

    private static DateTime? TryGetNodeStartedAtUtc(NodeInstance node)
    {
        return ReadDateTime(node, "StartedAtUtc")
               ?? ReadDateTime(node, "StartedAt")
               ?? ReadDateTime(node, "ActivatedAtUtc")
               ?? ReadDateTime(node, "ActivatedAt");
    }

    private static Guid? TryGetNodeScopeId(NodeInstance node)
    {
        return ReadGuid(node, "ScopeId")
               ?? ReadGuid(node, "TokenScopeId");
    }

    private static DateTime? ReadDateTime(object obj, string prop)
    {
        var p = obj.GetType().GetProperty(prop,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.IgnoreCase);

        if (p is null) return null;

        var v = p.GetValue(obj);
        if (v is DateTime dt) return dt;
        return null;
    }

    private static Guid? ReadGuid(object obj, string prop)
    {
        var p = obj.GetType().GetProperty(prop,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.IgnoreCase);

        if (p is null) return null;

        var v = p.GetValue(obj);
        if (v is Guid g) return g;
        return null;
    }

    // =========================================================
    // Reflection primitive getters
    // =========================================================

    private static bool? ReadBool(object obj, string prop)
    {
        var p = obj.GetType().GetProperty(prop,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.IgnoreCase);

        if (p is null) return null;

        var v = p.GetValue(obj);
        if (v is bool b) return b;
        return null;
    }

    private static object? ReadObject(object obj, string prop)
    {
        var p = obj.GetType().GetProperty(prop,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.IgnoreCase);

        return p?.GetValue(obj);
    }

    private static string? ReadString(object obj, string prop)
    {
        var p = obj.GetType().GetProperty(prop,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.IgnoreCase);

        return p?.GetValue(obj) as string;
    }
}
