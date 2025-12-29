using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Entities;

/// <summary>
/// Stores minimal execution data for each BPMN node that was executed
/// Tracks the execution path from start to end events with only IsExecutable=true nodes
/// </summary>
public sealed class ExecutedNode : BaseAggregateRoot
{
    /// <summary>
    /// Foreign key to the Process
    /// </summary>
    public Guid ProcessId { get; private set; }

    /// <summary>
    /// BPMN element ID (unique within the process)
    /// </summary>
    public string NodeId { get; private set; } = default!;

    /// <summary>
    /// Human-readable node name
    /// </summary>
    public string? NodeName { get; private set; }

    /// <summary>
    /// BPMN element type (StartEvent, UserTask, EndEvent, etc.)
    /// </summary>
    public string NodeType { get; private set; } = default!;

    /// <summary>
    /// When this node was executed
    /// </summary>
    public DateTime ExecutedAt { get; private set; }

    /// <summary>
    /// Sequence order in the execution path (1, 2, 3, ...)
    /// </summary>
    public int SequenceOrder { get; private set; }

    /// <summary>
    /// Previous node ID in the execution path (for path reconstruction)
    /// </summary>
    public string? PreviousNodeId { get; private set; }

    /// <summary>
    /// The token that executed this node
    /// </summary>
    public Guid TokenId { get; private set; }

    /// <summary>
    /// Execution scope/cycle ID
    /// </summary>
    public Guid? ScopeId { get; private set; }

    /// <summary>
    /// Whether this node execution was completed successfully
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Flow ID that led to this node (for path tracking)
    /// </summary>
    public string? ArrivedViaFlowId { get; private set; }

    /// <summary>
    /// Activity instance ID if this node is part of a subprocess
    /// </summary>
    public Guid? ActivityInstanceId { get; private set; }

    // Navigation property
    public Process? Process { get; private set; }

    private ExecutedNode() { }

    public ExecutedNode(
        Guid processId,
        string nodeId,
        string? nodeName,
        string nodeType,
        Guid tokenId,
        Guid? scopeId,
        int sequenceOrder,
        string? previousNodeId = null,
        string? arrivedViaFlowId = null,
        Guid? activityInstanceId = null)
    {
        ProcessId = processId;
        NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        NodeName = nodeName;
        NodeType = nodeType ?? throw new ArgumentNullException(nameof(nodeType));
        TokenId = tokenId;
        ScopeId = scopeId;
        SequenceOrder = sequenceOrder;
        PreviousNodeId = previousNodeId;
        ArrivedViaFlowId = arrivedViaFlowId;
        ActivityInstanceId = activityInstanceId;
        ExecutedAt = DateTime.UtcNow;
        IsCompleted = false; // Will be set to true when node completes
    }

    /// <summary>
    /// Mark this node execution as completed
    /// </summary>
    public void MarkCompleted()
    {
        if (IsCompleted)
            return;

        IsCompleted = true;
    }

    /// <summary>
    /// Update the previous node reference (for path reconstruction)
    /// </summary>
    public void SetPreviousNode(string previousNodeId)
    {
        PreviousNodeId = previousNodeId;
    }

    /// <summary>
    /// Update the flow that led to this node
    /// </summary>
    public void SetArrivedViaFlow(string flowId)
    {
        ArrivedViaFlowId = flowId;
    }

    public void SetNodeName(string nodeName)
    {
        NodeName = nodeName;
    }

    public void SetNodeType(string nodeType)
    {
        NodeType = nodeType;
    }
}