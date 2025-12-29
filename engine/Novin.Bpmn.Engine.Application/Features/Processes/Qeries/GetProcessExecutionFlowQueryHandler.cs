using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Queries.GetProcessExecutionFlow;

public sealed class GetProcessExecutionFlowQueryHandler
    : IRequestHandler<GetProcessExecutionFlowQuery, ProcessExecutionFlowDto?>
{
    private readonly IProcessRepository _processRepository;
    private readonly ITokenRepository _tokenRepository;
    private readonly INodeInstanceRepository _nodeInstances;
    private readonly IBpmnRuntimeContextFactory _ctxFactory;
    private readonly ILogger<GetProcessExecutionFlowQueryHandler> _logger;

    public GetProcessExecutionFlowQueryHandler(
        IProcessRepository processRepository,
        ITokenRepository tokenRepository,
        INodeInstanceRepository nodeInstances,
        IBpmnRuntimeContextFactory ctxFactory,
        ILogger<GetProcessExecutionFlowQueryHandler> logger)
    {
        _processRepository = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
        _tokenRepository = tokenRepository ?? throw new ArgumentNullException(nameof(tokenRepository));
        _nodeInstances = nodeInstances ?? throw new ArgumentNullException(nameof(nodeInstances));
        _ctxFactory = ctxFactory ?? throw new ArgumentNullException(nameof(ctxFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProcessExecutionFlowDto?> Handle(GetProcessExecutionFlowQuery request, CancellationToken ct)
    {
        if (request.ProcessId == Guid.Empty)
            return null;

        // 1) Process
        var process = await _processRepository.GetByIdAsync(request.ProcessId, ct);
        if (process is null)
        {
            _logger.LogWarning("Process not found. ProcessId={ProcessId}", request.ProcessId);
            return null;
        }

        // 2) NodeInstances (اینجا منبع اصلی executed nodes + state هست)
        var nodes = (await _nodeInstances.GetByProcessIdAsync(process.Id, ct)).ToList();

        // 3) Tokens فقط برای TotalTokens (اگر نمیخوای، میتونی unique TokenId از nodes بگیری)
        var tokens = (await _tokenRepository.GetByProcessIdAsync(process.Id, ct)).ToList();

        // 4) Load BPMN definitions/model for names/types + sequence flows
        // (برای اینکه از روی deployment definitions، node/flow details رو پر کنیم)
        BpmnRuntimeContext? ctx = null;
        string bpmnProcessId = process.ProcessBpmnId;

        // elementId -> element
        var elementById = new Dictionary<string, BpmnFlowElement>(StringComparer.Ordinal);

        // flowId -> sequenceFlow
        var flowById = new Dictionary<string, BpmnSequenceFlow>(StringComparer.Ordinal);

        try
        {
            ctx = await _ctxFactory.CreateAsync(process, ct);
            if (ctx is not null)
            {
                bpmnProcessId = ctx.BpmnProcessId;

                foreach (var e in ctx.Model.GetFlowElements(bpmnProcessId).Where(x => !string.IsNullOrWhiteSpace(x.id)))
                    elementById[e.id!] = e;

                foreach (var f in ctx.Model.GetSequenceFlows(bpmnProcessId))
                {
                    var key = FlowKey(f);
                    if (!string.IsNullOrWhiteSpace(key))
                        flowById[key] = f;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create BPMN runtime context. ProcessId={ProcessId}", process.Id);
        }

        // ----------------------------
        // ExecutedElements (از روی NodeInstances + State)
        // ----------------------------
        // 4) Executed Elements (group by ElementId) - ✅ with single Status
        var executedElements = nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.ElementId))
            .GroupBy(n => n.ElementId, StringComparer.Ordinal)
            .Select(g =>
            {
                var elementId = g.Key;

                // time helpers
                static DateTime NodeTime(NodeInstance n) => n.StartedAtUtc ?? n.CreatedAtUtc;

                var firstExecutedAt = g.Min(NodeTime);

                // ✅ latest node instance decides Status
                var latestNode = g
                    .OrderByDescending(n => n.CompletedAtUtc ?? n.StartedAtUtc ?? n.CreatedAtUtc)
                    .First();

                var status = latestNode.State;

                // name/type from definitions
                string? name = null;
                string type = "Unknown";
                if (elementById.TryGetValue(elementId, out var el))
                {
                    name = ReadName(el);
                    type = GetElementType(el);
                }

                var tokenExecutions = g
                    .GroupBy(x => x.TokenId)
                    .Select(tg => new TokenExecutionDto
                    {
                        TokenId = tg.Key,
                        FirstExecutedAt = tg.Min(NodeTime),
                        LastExecutedAt = tg.Max(NodeTime),
                        ExecutionCount = tg.Count()
                    })
                    .OrderBy(x => x.FirstExecutedAt)
                    .ToList();

                return new ExecutedElementDto
                {
                    ElementId = elementId,
                    ElementType = type,
                    ElementName = name,
                    FirstExecutedAt = firstExecutedAt,

                    // ✅ renamed
                    CalculatedExecutionCount = g.Count(),

                    // ✅ single status
                    Status = status.ToString(),

                    TokenExecutions = tokenExecutions
                };
            })
            .OrderBy(x => x.FirstExecutedAt)
            .ToList();

        // ----------------------------
        // ExecutedFlows (فقط با ArrivedViaFlowId از NodeInstances)
        // هر NodeInstance مقصد یک flow هست => ArrivedViaFlowId همان flow اجرا شده است
        // ----------------------------
        var executedFlows = nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.ArrivedViaFlowId))
            .GroupBy(n => n.ArrivedViaFlowId!, StringComparer.Ordinal)
            .Select(g =>
            {
                var flowId = g.Key;

                string source = "";
                string target = "";
                string? name = null;
                string? condition = null;

                if (flowById.TryGetValue(flowId, out var f))
                {
                    source = f.sourceRef ?? "";
                    target = f.targetRef ?? "";
                    name = ReadName(f);
                    condition = ReadConditionExpression(f);
                }

                var firstAt = g.Min(NodeTime);

                var tokenExecutions = g
                    .GroupBy(x => x.TokenId)
                    .Select(tg => new TokenExecutionDto
                    {
                        TokenId = tg.Key,
                        FirstExecutedAt = tg.Min(NodeTime),
                        LastExecutedAt = tg.Max(NodeTime),
                        ExecutionCount = tg.Count()
                    })
                    .OrderBy(x => x.FirstExecutedAt)
                    .ToList();

                return new ExecutedFlowDto
                {
                    FlowId = flowId,
                    SourceElementId = source,
                    TargetElementId = target,
                    FlowName = name,
                    ConditionExpression = condition,
                    FirstExecutedAt = firstAt,
                    ExecutionCount = g.Count(),
                    TokenExecutions = tokenExecutions
                };
            })
            .OrderBy(x => x.FirstExecutedAt)
            .ToList();

        // ----------------------------
        // Stats
        // ----------------------------
        DateTime? minTime = nodes.Count > 0 ? nodes.Min(NodeTime) : (DateTime?)null;
        DateTime? maxTime = nodes.Count > 0 ? nodes.Max(NodeTime) : (DateTime?)null;

        var stats = new ExecutionStatsDto
        {
            TotalTokens = tokens.Count, // یا: nodes.Select(x=>x.TokenId).Distinct().Count()
            ExecutedElements = executedElements.Count,
            ExecutedFlows = executedFlows.Count,
            BoundaryEventsConfigured = 0,
            BoundaryEventsTriggered = 0,
            ExecutionCycles = 0,
            TotalExecutionTime = (minTime.HasValue && maxTime.HasValue && maxTime >= minTime)
                ? (maxTime.Value - minTime.Value)
                : null
        };

        return new ProcessExecutionFlowDto
        {
            ProcessId = process.Id,
            ProcessName = process.Name,
            ProcessBpmnId = process.ProcessBpmnId,
            DeploymentId = process.DeploymentId,
            State = process.State,
            CreatedAt = process.CreatedAtUtc,
            CompletedAt = process.CompletedAtUtc,
            ExecutedElements = executedElements,
            ExecutedFlows = executedFlows,
            BoundaryEvents = Array.Empty<BoundaryEventDto>(),
            ExecutionCycles = Array.Empty<ExecutionCycleDto>(),
            Stats = stats
        };
    }

    // ---------------- helpers ----------------

    private static DateTime NodeTime(NodeInstance n)
        => n.StartedAtUtc ?? n.CreatedAtUtc;

    private static string FlowKey(BpmnSequenceFlow f)
        => !string.IsNullOrWhiteSpace(f.id) ? f.id! : $"{f.sourceRef}->{f.targetRef}";

    private static string GetElementType(BpmnFlowElement e)
        => e.GetType().Name switch
        {
            "BpmnStartEvent" => "StartEvent",
            "BpmnEndEvent" => "EndEvent",
            "BpmnUserTask" => "UserTask",
            "BpmnServiceTask" => "ServiceTask",
            "BpmnScriptTask" => "ScriptTask",
            "BpmnBoundaryEvent" => "BoundaryEvent",
            "BpmnExclusiveGateway" => "ExclusiveGateway",
            "BpmnParallelGateway" => "ParallelGateway",
            "BpmnInclusiveGateway" => "InclusiveGateway",
            "BpmnEventBasedGateway" => "EventBasedGateway",
            _ => e.GetType().Name
        };

    private static string? ReadName(object obj)
    {
        var t = obj.GetType();
        var p = t.GetProperty("name") ?? t.GetProperty("Name") ?? t.GetProperty("label") ?? t.GetProperty("Label");
        return p?.GetValue(obj) as string;
    }

    private static string? ReadConditionExpression(BpmnSequenceFlow f)
    {
        // اگر در مدل شما conditionExpression موجود است:
        var p = f.GetType().GetProperty("conditionExpression", BindingFlags.Public | BindingFlags.Instance)
             ?? f.GetType().GetProperty("ConditionExpression", BindingFlags.Public | BindingFlags.Instance);

        return p?.GetValue(f) as string;
    }
}
