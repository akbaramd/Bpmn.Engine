using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class ProcessExecutionRecorder : IProcessExecutionRecorder
{
    private readonly IProcessExecutionNodeRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IBpmnRuntimeContextFactory _bpmnContextFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProcessExecutionRecorder> _logger;

    private const string CtxCachePrefix = "__bpmnctx:";

    public ProcessExecutionRecorder(
        IProcessExecutionNodeRepository repo,
        IUnitOfWork uow,
        IBpmnRuntimeContextFactory bpmnContextFactory,
        IMemoryCache cache,
        ILogger<ProcessExecutionRecorder> logger)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _bpmnContextFactory = bpmnContextFactory ?? throw new ArgumentNullException(nameof(bpmnContextFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RecordNodeExecutionAsync(
        Process process,
        Token token,
        string nodeId,
        string? arrivedViaFlowId = null,
        CancellationToken ct = default)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (token is null) throw new ArgumentNullException(nameof(token));
        if (string.IsNullOrWhiteSpace(nodeId)) throw new ArgumentNullException(nameof(nodeId));

        // Only record executable token executions
        if (!token.IsExecutable)
            return;

        // Resolve element info OUTSIDE trx
        var arrivedVia = arrivedViaFlowId ?? token.ArrivedViaFlowId;
        var (nodeName, nodeType) = await ResolveElementInfoSafeAsync(process, nodeId, ct);

        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            // 1) If exists -> update arrivedVia if needed and return
            var existing = await _repo.GetNodeAsync(process.Id, nodeId, trxCt);
            if (existing != null)
            {
                if (!string.IsNullOrWhiteSpace(arrivedVia) && string.IsNullOrWhiteSpace(existing.ArrivedViaFlowId))
                {
                    existing.SetArrivedViaFlow(arrivedVia);
                    await _repo.UpdateAsync(existing, trxCt);
                }

                // Optional: اگر nodeName/type قبلاً خالی بوده و الان داریم، پرش کن
                if (string.IsNullOrWhiteSpace(existing.NodeName) && !string.IsNullOrWhiteSpace(nodeName))
                {
                    existing.SetNodeName(nodeName);
                    await _repo.UpdateAsync(existing, trxCt);
                }
                if (string.IsNullOrWhiteSpace(existing.NodeType) && !string.IsNullOrWhiteSpace(nodeType))
                {
                    existing.SetNodeType(nodeType);
                    await _repo.UpdateAsync(existing, trxCt);
                }

                return;
            }

            // 2) Compute sequence order + previous node
            var lastNode = await _repo.GetLastExecutedNodeAsync(process.Id, trxCt);
            var sequenceOrder = (lastNode?.SequenceOrder ?? 0) + 1;
            var previousNodeId = lastNode?.NodeId;

            // 3) Insert new executed node
            var executionNode = new ExecutedNode(
                processId: process.Id,
                nodeId: nodeId,
                nodeName: nodeName,
                nodeType: nodeType,
                tokenId: token.Id,
                scopeId: token.ScopeId,
                sequenceOrder: sequenceOrder,
                previousNodeId: previousNodeId,
                arrivedViaFlowId: arrivedVia,
                activityInstanceId: token.ActivityInstanceId
            );

            await _repo.AddAsync(executionNode, trxCt);
        }, ct);
    }

    public async Task MarkNodeCompletedAsync(Guid processId, string nodeId, CancellationToken ct = default)
    {
        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            var node = await _repo.GetNodeAsync(processId, nodeId, trxCt);
            if (node != null && !node.IsCompleted)
            {
                node.MarkCompleted();
                await _repo.UpdateAsync(node, trxCt);
            }
        }, ct);
    }

    public Task<IEnumerable<ExecutedNode>> GetExecutionPathAsync(Guid processId, CancellationToken ct = default)
        => _repo.GetExecutionPathAsync(processId, ct);

    public Task<ProcessExecutionStats> GetExecutionStatsAsync(Guid processId, CancellationToken ct = default)
        => _repo.GetExecutionStatsAsync(processId, ct);

    // ------------------ Element info resolution ------------------

    private async Task<(string nodeName, string nodeType)> ResolveElementInfoSafeAsync(
        Process process,
        string elementId,
        CancellationToken ct)
    {
        try
        {
            var ctx = await GetOrCreateContextAsync(process, ct);
            var element = ctx.Model.GetElementById(ctx.BpmnProcessId, elementId);

            if (element == null)
                return (string.Empty, "Unknown");

            var name = GetElementName(element) ?? string.Empty;
            var type = GetElementType(element);

            return (name, type);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[REC] Failed to resolve element info. ProcessId={Pid} ElementId={Eid}",
                process.Id, elementId);

            return (string.Empty, "Unknown");
        }
    }

    private async Task<BpmnRuntimeContext> GetOrCreateContextAsync(Process process, CancellationToken ct)
    {
        var key = $"{CtxCachePrefix}{process.DeploymentId:N}:{process.ProcessBpmnId}";

        if (_cache.TryGetValue(key, out BpmnRuntimeContext cached))
            return cached;

        var ctx = await _bpmnContextFactory.CreateAsync(process, ct);

        // نکته: Size فقط وقتی مجاز است که MemoryCache SizeLimit داشته باشد
        _cache.Set(key, ctx, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(20)
        });

        return ctx;
    }

    private static string? GetElementName(object element)
    {
        var t = element.GetType();
        var p =
            t.GetProperty("name") ??
            t.GetProperty("Name") ??
            t.GetProperty("label") ??
            t.GetProperty("Label");

        return p?.GetValue(element) as string;
    }

    private static string GetElementType(object element)
    {
        return element switch
        {
            BpmnStartEvent => "startEvent",
            BpmnEndEvent => "endEvent",
            BpmnExclusiveGateway => "exclusiveGateway",
            BpmnInclusiveGateway => "inclusiveGateway",
            BpmnParallelGateway => "parallelGateway",
            BpmnEventBasedGateway => "eventBasedGateway",
            BpmnServiceTask => "serviceTask",
            BpmnScriptTask => "scriptTask",
            BpmnUserTask => "userTask",
            BpmnSubProcess => "subProcess",
            _ => element.GetType().Name
        };
    }
}
