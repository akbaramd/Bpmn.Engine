using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Service for recording minimal execution data for BPMN processes
/// Tracks only IsExecutable=true nodes from start to end events
/// </summary>
public interface IProcessExecutionRecorder
{
    /// <summary>
    /// Record when a node is executed (only for IsExecutable=true tokens)
    /// </summary>
    Task RecordNodeExecutionAsync(
        Process process,
        Token token,
        string nodeId,
        string nodeName,
        string nodeType,
        string? arrivedViaFlowId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Mark a node as completed when its execution finishes
    /// </summary>
    Task MarkNodeCompletedAsync(
        Guid processId,
        string nodeId,
        CancellationToken ct = default);

    /// <summary>
    /// Get the execution path for a process
    /// </summary>
    Task<IEnumerable<ProcessExecutionNode>> GetExecutionPathAsync(
        Guid processId,
        CancellationToken ct = default);

    /// <summary>
    /// Get execution statistics for a process
    /// </summary>
    Task<ProcessExecutionStats> GetExecutionStatsAsync(
        Guid processId,
        CancellationToken ct = default);
}

/// <summary>
/// Implementation of ProcessExecutionRecorder
/// </summary>
public class ProcessExecutionRecorder : IProcessExecutionRecorder
{
    private readonly IProcessExecutionNodeRepository _executionNodeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProcessExecutionRecorder(
        IProcessExecutionNodeRepository executionNodeRepository,
        IUnitOfWork unitOfWork)
    {
        _executionNodeRepository = executionNodeRepository ?? throw new ArgumentNullException(nameof(executionNodeRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task RecordNodeExecutionAsync(
        Process process,
        Token token,
        string nodeId,
        string nodeName,
        string nodeType,
        string? arrivedViaFlowId = null,
        CancellationToken ct = default)
    {
        // Only record executable token executions
        if (!token.IsExecutable)
        {
            return;
        }

        // Check if this node was already executed for this process
        var existingNodes = await _executionNodeRepository.GetByProcessIdAsync(process.Id, ct);
        var existingNode = existingNodes.FirstOrDefault(n => n.NodeId == nodeId);

        if (existingNode != null)
        {
            // Node already executed, just update if needed
            if (!string.IsNullOrEmpty(arrivedViaFlowId) && existingNode.ArrivedViaFlowId == null)
            {
                existingNode.SetArrivedViaFlow(arrivedViaFlowId);
                await _executionNodeRepository.UpdateAsync(existingNode, ct);
            }
            return;
        }

        // Get the next sequence order
        var lastNode = await _executionNodeRepository.GetLastExecutedNodeAsync(process.Id, ct);
        var sequenceOrder = lastNode?.SequenceOrder + 1 ?? 1;

        // Find previous node in the path (for path reconstruction)
        var previousNodeId = lastNode?.NodeId;

        var executionNode = new ProcessExecutionNode(
            processId: process.Id,
            nodeId: nodeId,
            nodeName: nodeName,
            nodeType: nodeType,
            tokenId: token.Id,
            scopeId: token.ScopeId,
            sequenceOrder: sequenceOrder,
            previousNodeId: previousNodeId,
            arrivedViaFlowId: arrivedViaFlowId,
            activityInstanceId: token.ActivityInstanceId);

        await _executionNodeRepository.AddAsync(executionNode, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task MarkNodeCompletedAsync(
        Guid processId,
        string nodeId,
        CancellationToken ct = default)
    {
        var nodes = await _executionNodeRepository.GetByProcessIdAsync(processId, ct);
        var node = nodes.FirstOrDefault(n => n.NodeId == nodeId);

        if (node != null && !node.IsCompleted)
        {
            node.MarkCompleted();
            await _executionNodeRepository.UpdateAsync(node, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<IEnumerable<ProcessExecutionNode>> GetExecutionPathAsync(
        Guid processId,
        CancellationToken ct = default)
    {
        return await _executionNodeRepository.GetExecutionPathAsync(processId, ct);
    }

    public async Task<ProcessExecutionStats> GetExecutionStatsAsync(
        Guid processId,
        CancellationToken ct = default)
    {
        return await _executionNodeRepository.GetExecutionStatsAsync(processId, ct);
    }
}