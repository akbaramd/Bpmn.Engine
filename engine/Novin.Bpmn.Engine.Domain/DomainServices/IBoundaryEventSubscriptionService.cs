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
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IBoundarySubscriptionRepository _boundarySubscriptionRepository;
    private readonly IBpmnQuery _bpmnQuery;
    private readonly ILogger<BoundaryEventSubscriptionService> _logger;

    public BoundaryEventSubscriptionService(
        IProcessRepository processRepository,
        IDeploymentRepository deploymentRepository,
        IBoundarySubscriptionRepository boundarySubscriptionRepository,
        IBpmnQuery bpmnQuery,
        ILogger<BoundaryEventSubscriptionService> logger)
    {
        _processRepository = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
        _deploymentRepository = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
        _boundarySubscriptionRepository = boundarySubscriptionRepository ?? throw new ArgumentNullException(nameof(boundarySubscriptionRepository));
        _bpmnQuery = bpmnQuery ?? throw new ArgumentNullException(nameof(bpmnQuery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<BoundaryEventSubscription>> SubscribeBoundaryEventsAsync(NodeInstance node, CancellationToken ct)
    {
        if (node is null) throw new ArgumentNullException(nameof(node));
        if (string.IsNullOrWhiteSpace(node.ElementId))
            return Array.Empty<BoundaryEventSubscription>();

        // 1) Load process + deployment
        var process = await _processRepository.GetByIdAsync(node.ProcessId);
        if (process is null)
        {
            _logger.LogWarning("SubscribeBoundaryEventsAsync: Process not found. ProcessId={ProcessId}", node.ProcessId);
            return Array.Empty<BoundaryEventSubscription>();
        }

        var deployment = await _deploymentRepository.GetByIdAsync(process.DeploymentId);
        if (deployment is null)
        {
            _logger.LogWarning("SubscribeBoundaryEventsAsync: Deployment not found. DeploymentId={DeploymentId}", process.DeploymentId);
            return Array.Empty<BoundaryEventSubscription>();
        }

        // 2) Load boundary events from BPMN model
        var boundaryEvents = _bpmnQuery.GetAllElementsOfType<BpmnBoundaryEvent>(deployment, process.ProcessBpmnId);
        if (boundaryEvents is null || boundaryEvents.Count == 0)
            return Array.Empty<BoundaryEventSubscription>();

        var created = new List<BoundaryEventSubscription>();

        foreach (var boundaryEvent in boundaryEvents)
        {
            ct.ThrowIfCancellationRequested();

            // 3) Match attachedToRef to node.ElementId (در XML تو attachedToRef یک string attribute است)
            var attachedToId = ReadString(boundaryEvent, "attachedToRef")
                               ?? ReadString(ReadObject(boundaryEvent, "attachedToRef") ?? new object(), "id");

            if (string.IsNullOrWhiteSpace(attachedToId))
                continue;

            if (!string.Equals(attachedToId, node.ElementId, StringComparison.Ordinal))
                continue;

            // 4) Detect boundary kind + extract definition (Timer/Error/Message)
            if (!TryExtractBoundaryDefinition(boundaryEvent,
                    out var kind,
                    out var timerType,
                    out var timerExpr,
                    out var errorCode,
                    out var messageName))
            {
                continue;
            }

            // 5) Idempotency: avoid duplicates for same node-instance + boundary element while Active
            var boundaryElementId = ReadString(boundaryEvent, "id");
            if (string.IsNullOrWhiteSpace(boundaryElementId))
                continue;

            var exists = await _boundarySubscriptionRepository.ExistsActiveAsync(
                processId: process.Id,
                nodeInstanceId: node.Id,
                boundaryElementId: boundaryElementId,
                ct: ct);

            if (exists)
                continue;

            // 6) Timer compute DueAt (required for Quartz scheduling)
            DateTimeOffset? dueAt = null;
            DateTimeOffset? nextDueAtUtc = null;

            if (kind == BoundaryKind.Timer)
            {
                if (timerType is null || string.IsNullOrWhiteSpace(timerExpr))
                    continue;

                var startedAtUtc = TryGetNodeStartedAtUtc(node);

                // timeDuration/timeCycle نیازمند anchor time هستند
                if (timerType is TimerType.TimeDuration or TimerType.TimeCycle)
                {
                    if (startedAtUtc is null)
                    {
                        _logger.LogWarning(
                            "Timer boundary requires node.StartedAtUtc (or equivalent). NodeId={NodeId} ElementId={ElementId} BoundaryId={BoundaryId}",
                            node.Id, node.ElementId, boundaryElementId);
                        continue;
                    }
                }

                dueAt = ComputeDueAt(timerType.Value, timerExpr!, startedAtUtc);
                if (!dueAt.HasValue)
                {
                    _logger.LogWarning(
                        "Failed to compute DueAt for timer boundary. NodeId={NodeId} ElementId={ElementId} BoundaryId={BoundaryId} Expr={Expr}",
                        node.Id, node.ElementId, boundaryElementId, timerExpr);
                    continue;
                }

                if (timerType == TimerType.TimeCycle)
                    nextDueAtUtc = dueAt;
            }

            // 7) interrupting flag (cancelActivity attribute)
            var cancelActivity = ReadBool(boundaryEvent, "cancelActivity") ?? false;

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

            // optional: ثبت source برای debugging
            // sub.SetMeta("source", "node-created-subscribe");

            await _boundarySubscriptionRepository.AddAsync(sub, ct);
            created.Add(sub);

            _logger.LogDebug(
                "Boundary subscription created. Kind={Kind} Host={Host} Boundary={Boundary} NodeInstanceId={NodeInstanceId} Interrupting={Interrupting}",
                kind, node.ElementId, boundaryElementId, node.Id, cancelActivity);
        }

        return created;
    }

    // =========================================================
    // Boundary definition extraction
    // =========================================================

    private static bool TryExtractBoundaryDefinition(
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

        // ---- TIMER ---- (matches your XML: <timerEventDefinition><timeDuration>PT10S</timeDuration>)
        var timerDef = ReadObject(e, "timerEventDefinition") ?? ReadObject(e, "timerEventDefinitions");
        if (timerDef is not null)
        {
            timerDef = FirstOrSelf(timerDef);

            var timeDate = ReadExpressionText(timerDef, "timeDate");
            var timeDuration = ReadExpressionText(timerDef, "timeDuration");
            var timeCycle = ReadExpressionText(timerDef, "timeCycle");

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

            return false;
        }

        // ---- ERROR ---- (matches your XML: <errorEventDefinition .../>)
        var errDef = ReadObject(e, "errorEventDefinition") ?? ReadObject(e, "errorEventDefinitions");
        if (errDef is not null)
        {
            errDef = FirstOrSelf(errDef);

            // در XML تو errorRef نیامده، پس errorCode ممکن است null بماند (OK)
            errorCode =
                ReadString(errDef, "errorCode")
                ?? ReadString(ReadObject(errDef, "errorRef") ?? new object(), "errorCode")
                ?? ReadString(ReadObject(errDef, "errorRef") ?? new object(), "id")
                ?? ReadString(errDef, "id");

            kind = BoundaryKind.Error;
            return true;
        }

        // ---- MESSAGE ---- (اگر بعداً اضافه کردی)
        var msgDef = ReadObject(e, "messageEventDefinition") ?? ReadObject(e, "messageEventDefinitions");
        if (msgDef is not null)
        {
            msgDef = FirstOrSelf(msgDef);

            messageName =
                ReadString(msgDef, "messageName")
                ?? ReadString(ReadObject(msgDef, "messageRef") ?? new object(), "name")
                ?? ReadString(ReadObject(msgDef, "messageRef") ?? new object(), "id");

            kind = BoundaryKind.Message;
            return true;
        }

        return false;
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
