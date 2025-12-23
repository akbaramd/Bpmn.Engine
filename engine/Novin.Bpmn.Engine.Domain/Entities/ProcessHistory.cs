using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Domain.Entities;

/// <summary>
/// Entity representing a history entry in a process execution
/// </summary>
public class ProcessHistory : BaseEntity
{
    public Guid ProcessId { get; private set; }
    public Guid NodeId { get; private set; }
    public string ElementId { get; private set; }
    public string NodeName { get; private set; }
    public NodeState State { get; private set; }
    public Guid? TokenId { get; private set; }
    public DateTime ExecutedAt { get; private set; }

    private ProcessHistory() : base()
    {
    }

    public ProcessHistory(
        Guid processId,
        Guid nodeId,
        string elementId,
        string nodeName,
        NodeState state,
        Guid? tokenId = null) : base()
    {
        if (processId == Guid.Empty)
            throw new ArgumentException("Process ID cannot be empty", nameof(processId));

        if (nodeId == Guid.Empty)
            throw new ArgumentException("Node ID cannot be empty", nameof(nodeId));

        if (string.IsNullOrWhiteSpace(elementId))
            throw new ArgumentException("Element ID cannot be null or empty", nameof(elementId));

        if (string.IsNullOrWhiteSpace(nodeName))
            throw new ArgumentException("Node name cannot be null or empty", nameof(nodeName));

        ProcessId = processId;
        NodeId = nodeId;
        ElementId = elementId;
        NodeName = nodeName;
        State = state;
        TokenId = tokenId;
        ExecutedAt = DateTime.UtcNow;
    }
}

