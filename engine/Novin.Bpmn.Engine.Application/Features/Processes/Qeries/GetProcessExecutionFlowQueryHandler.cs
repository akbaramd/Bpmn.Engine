using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
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
    private readonly IExecutionFlowRepository _executionFlows;
    private readonly IBpmnRuntimeContextFactory _ctxFactory;
    private readonly ILogger<GetProcessExecutionFlowQueryHandler> _logger;

    public GetProcessExecutionFlowQueryHandler(
        IProcessRepository processRepository,
        ITokenRepository tokenRepository,
        INodeInstanceRepository nodeInstances,
        IExecutionFlowRepository executionFlows,
        IBpmnRuntimeContextFactory ctxFactory,
        ILogger<GetProcessExecutionFlowQueryHandler> logger)
    {
        _processRepository = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
        _tokenRepository = tokenRepository ?? throw new ArgumentNullException(nameof(tokenRepository));
        _nodeInstances = nodeInstances ?? throw new ArgumentNullException(nameof(nodeInstances));
        _executionFlows = executionFlows ?? throw new ArgumentNullException(nameof(executionFlows));
        _ctxFactory = ctxFactory ?? throw new ArgumentNullException(nameof(ctxFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProcessExecutionFlowDto?> Handle(GetProcessExecutionFlowQuery request, CancellationToken ct)
    {
        if (request.ProcessId == Guid.Empty)
            return null;

        var process = await _processRepository.GetByIdAsync(request.ProcessId, ct);
        if (process is null)
            return null;

        var nodes = (await _nodeInstances.GetByProcessIdAsync(process.Id, ct)).ToList();
        var tokens = (await _tokenRepository.GetByProcessIdAsync(process.Id, ct)).ToList();
        var flowRecords = await _executionFlows.GetByProcessIdAsync(process.Id, ct);

        // ---- BPMN context (best-effort) ----
        var elementById = new Dictionary<string, BpmnFlowElement>(StringComparer.Ordinal);
        var flowByKey = new Dictionary<string, BpmnSequenceFlow>(StringComparer.Ordinal);
        var bpmnProcessId = process.ProcessBpmnId;

        try
        {
            var ctx = await _ctxFactory.CreateAsync(process, ct);
            if (ctx is not null)
            {
                bpmnProcessId = ctx.BpmnProcessId;

                foreach (var el in ctx.Model.GetFlowElements(bpmnProcessId))
                {
                    if (!string.IsNullOrWhiteSpace(el.id))
                        elementById[el.id!] = el;
                }

                foreach (var f in ctx.Model.GetSequenceFlows(bpmnProcessId))
                {
                    var k1 = FlowKey(f);
                    if (!string.IsNullOrWhiteSpace(k1) && !flowByKey.ContainsKey(k1))
                        flowByKey[k1] = f;

                    // also index by source->target to support older persisted keys
                    var k2 = $"{f.sourceRef}->{f.targetRef}";
                    if (!string.IsNullOrWhiteSpace(k2) && !flowByKey.ContainsKey(k2))
                        flowByKey[k2] = f;

                    // also index by id if present (even if FlowKey chose other)
                    if (!string.IsNullOrWhiteSpace(f.id) && !flowByKey.ContainsKey(f.id!))
                        flowByKey[f.id!] = f;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create BPMN runtime context. ProcessId={ProcessId}", process.Id);
        }

        // ----------------------------
        // Executed Elements (NodeInstances authoritative)
        // ----------------------------
        var executedElements = BuildExecutedElements(nodes, elementById);

        // ----------------------------
        // Executed Flows (ExecutionFlowRecord authoritative)
        // ----------------------------
        var executedFlows = BuildExecutedFlows(flowRecords, flowByKey);

        // ----------------------------
        // Stats
        // ----------------------------
        DateTime? minNode = nodes.Count > 0 ? nodes.Min(NodeTime) : null;
        DateTime? maxNode = nodes.Count > 0 ? nodes.Max(NodeLastTime) : null;

        DateTime? minFlow = flowRecords.Count > 0 ? flowRecords.Min(r => r.OccurredAtUtc) : null;
        DateTime? maxFlow = flowRecords.Count > 0 ? flowRecords.Max(r => r.OccurredAtUtc) : null;

        var minTime = MinNullable(minNode, minFlow);
        var maxTime = MaxNullable(maxNode, maxFlow);

        var stats = new ExecutionStatsDto
        {
            TotalTokens = tokens.Count,
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

    // ----------------------------
    // Build Executed Elements
    // ----------------------------
    private static List<ExecutedElementDto> BuildExecutedElements(
        List<NodeInstance> nodes,
        Dictionary<string, BpmnFlowElement> elementById)
    {
        if (nodes.Count == 0)
            return new List<ExecutedElementDto>(0);

        var grouped = new Dictionary<string, List<NodeInstance>>(StringComparer.Ordinal);

        for (var i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (string.IsNullOrWhiteSpace(n.ElementId)) continue;

            if (!grouped.TryGetValue(n.ElementId, out var list))
            {
                list = new List<NodeInstance>(capacity: 4);
                grouped[n.ElementId] = list;
            }
            list.Add(n);
        }

        var result = new List<ExecutedElementDto>(grouped.Count);

        foreach (var kv in grouped)
        {
            var elementId = kv.Key;
            var list = kv.Value;

            // first executed at
            DateTime firstAt = DateTime.MaxValue;
            for (var i = 0; i < list.Count; i++)
            {
                var t = NodeTime(list[i]);
                if (t < firstAt) firstAt = t;
            }

            // latest node decides status/details
            NodeInstance latest = list[0];
            DateTime latestKey = NodeLastTime(latest);

            for (var i = 1; i < list.Count; i++)
            {
                var n = list[i];
                var k = NodeLastTime(n);
                if (k > latestKey)
                {
                    latest = n;
                    latestKey = k;
                }
            }

            string? name = null;
            string type = "Unknown";
            if (elementById.TryGetValue(elementId, out var el))
            {
                name = ReadName(el);
                type = GetElementType(el);
            }

            // token executions
            var byToken = new Dictionary<Guid, TokenExecutionDto>();
            for (var i = 0; i < list.Count; i++)
            {
                var n = list[i];
                if (!byToken.TryGetValue(n.TokenId, out var te))
                {
                    te = new TokenExecutionDto
                    {
                        TokenId = n.TokenId,
                        FirstExecutedAt = NodeTime(n),
                        LastExecutedAt = NodeTime(n),
                        ExecutionCount = 0
                    };
                    byToken[n.TokenId] = te;
                }

                var nt = NodeTime(n);
                if (nt < te.FirstExecutedAt) te.FirstExecutedAt = nt;
                if (nt > te.LastExecutedAt) te.LastExecutedAt = nt;
                te.ExecutionCount += 1;
            }

            var tokenExecutions = byToken.Values
                .OrderBy(x => x.FirstExecutedAt)
                .ToList();

            // variables (latest wins)
            var mergedVars = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
            list.Sort((a, b) => NodeLastTime(b).CompareTo(NodeLastTime(a)));
            for (var i = 0; i < list.Count; i++)
            {
                var n = list[i];
                foreach (var kvp in n.VariablesObject)
                {
                    if (!mergedVars.ContainsKey(kvp.Key))
                        mergedVars[kvp.Key] = kvp.Value;
                }
            }

            result.Add(new ExecutedElementDto
            {
                ElementId = elementId,
                ElementType = type,
                ElementName = name,
                FirstExecutedAt = firstAt,
                CalculatedExecutionCount = list.Count,
                Status = latest.State.ToString(),
                NodeInstanceId = latest.Id,
                ScopeId = latest.ScopeId,
                ActivityInstanceId = latest.ActivityInstanceId,
                ArrivedViaFlowIds = latest.ArrivedViaFlowIds?.ToList() ?? new List<string>(),
                StartedAtUtc = latest.StartedAtUtc,
                CompletedAtUtc = latest.CompletedAtUtc,
                ErrorMessage = latest.ErrorMessage,
                Variables = mergedVars,
                TokenExecutions = tokenExecutions
            });
        }

        result.Sort((a, b) => a.FirstExecutedAt.CompareTo(b.FirstExecutedAt));
        return result;
    }

    // ----------------------------
    // Build Executed Flows
    // ----------------------------
    private static List<ExecutedFlowDto> BuildExecutedFlows(
        IReadOnlyList<Domain.Entities.ExecutionFlowRecord> records,
        Dictionary<string, BpmnSequenceFlow> flowByKey)
    {
        if (records.Count == 0)
            return new List<ExecutedFlowDto>(0);

        var agg = new Dictionary<string, FlowAgg>(StringComparer.Ordinal);

        for (var i = 0; i < records.Count; i++)
        {
            var r = records[i];
            var via = r.ViaFlowIds;
            if (via is null || via.Count == 0) continue;

            for (var j = 0; j < via.Count; j++)
            {
                var flowId = via[j];
                if (string.IsNullOrWhiteSpace(flowId)) continue;

                if (!agg.TryGetValue(flowId, out var a))
                {
                    a = new FlowAgg(flowId)
                    {
                        FirstAt = r.OccurredAtUtc,
                        FallbackFrom = r.FromElementId,
                        FallbackTo = r.ToElementId
                    };
                    agg[flowId] = a;
                }

                if (r.OccurredAtUtc < a.FirstAt) a.FirstAt = r.OccurredAtUtc;

                a.ExecutionCount += 1;

                if (!a.ByToken.TryGetValue(r.TokenId, out var te))
                {
                    te = new TokenExecutionDto
                    {
                        TokenId = r.TokenId,
                        FirstExecutedAt = r.OccurredAtUtc,
                        LastExecutedAt = r.OccurredAtUtc,
                        ExecutionCount = 0
                    };
                    a.ByToken[r.TokenId] = te;
                }

                if (r.OccurredAtUtc < te.FirstExecutedAt) te.FirstExecutedAt = r.OccurredAtUtc;
                if (r.OccurredAtUtc > te.LastExecutedAt) te.LastExecutedAt = r.OccurredAtUtc;
                te.ExecutionCount += 1;
            }
        }

        var list = new List<ExecutedFlowDto>(agg.Count);

        foreach (var a in agg.Values.OrderBy(x => x.FirstAt))
        {
            string source = a.FallbackFrom;
            string target = a.FallbackTo;
            string? name = null;
            string? condition = null;

            if (flowByKey.TryGetValue(a.FlowId, out var f))
            {
                source = f.sourceRef ?? source;
                target = f.targetRef ?? target;
                name = ReadName(f);
                condition = ReadConditionExpression(f);
            }

            list.Add(new ExecutedFlowDto
            {
                FlowId = a.FlowId,
                SourceElementId = source,
                TargetElementId = target,
                FlowName = name,
                ConditionExpression = condition,
                FirstExecutedAt = a.FirstAt,
                ExecutionCount = a.ExecutionCount,
                TokenExecutions = a.ByToken.Values.OrderBy(x => x.FirstExecutedAt).ToList()
            });
        }

        return list;
    }

    private sealed class FlowAgg
    {
        public FlowAgg(string flowId)
        {
            FlowId = flowId;
        }

        public string FlowId { get; }
        public DateTime FirstAt { get; set; } = DateTime.MaxValue;
        public int ExecutionCount { get; set; }
        public string FallbackFrom { get; set; } = "";
        public string FallbackTo { get; set; } = "";
        public Dictionary<Guid, TokenExecutionDto> ByToken { get; } = new();
    }

    // ---------------- helpers ----------------

    private static DateTime NodeTime(NodeInstance n)
        => n.StartedAtUtc ?? n.CreatedAtUtc;

    private static DateTime NodeLastTime(NodeInstance n)
        => n.CompletedAtUtc ?? n.StartedAtUtc ?? n.CreatedAtUtc;

    private static DateTime? MinNullable(DateTime? a, DateTime? b)
        => a.HasValue && b.HasValue ? (a.Value <= b.Value ? a : b) : (a ?? b);

    private static DateTime? MaxNullable(DateTime? a, DateTime? b)
        => a.HasValue && b.HasValue ? (a.Value >= b.Value ? a : b) : (a ?? b);

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
        var p =
            t.GetProperty("name", BindingFlags.Public | BindingFlags.Instance)
            ?? t.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)
            ?? t.GetProperty("label", BindingFlags.Public | BindingFlags.Instance)
            ?? t.GetProperty("Label", BindingFlags.Public | BindingFlags.Instance);

        var v = p?.GetValue(obj) as string;
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private static string? ReadConditionExpression(BpmnSequenceFlow f)
    {
        var ceProp =
            f.GetType().GetProperty("conditionExpression", BindingFlags.Public | BindingFlags.Instance)
            ?? f.GetType().GetProperty("ConditionExpression", BindingFlags.Public | BindingFlags.Instance);

        var ce = ceProp?.GetValue(f);
        if (ce is null) return null;

        var textProp =
            ce.GetType().GetProperty("Text", BindingFlags.Public | BindingFlags.Instance)
            ?? ce.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);

        var textVal = textProp?.GetValue(ce);
        if (textVal is null) return null;

        if (textVal is string s)
        {
            var x = s.Trim();
            return string.IsNullOrWhiteSpace(x) ? null : x;
        }

        if (textVal is string[] arr)
        {
            var x = string.Concat(arr).Trim();
            return string.IsNullOrWhiteSpace(x) ? null : x;
        }

        if (textVal is IEnumerable<string> en)
        {
            var x = string.Concat(en).Trim();
            return string.IsNullOrWhiteSpace(x) ? null : x;
        }

        return null;
    }
}
